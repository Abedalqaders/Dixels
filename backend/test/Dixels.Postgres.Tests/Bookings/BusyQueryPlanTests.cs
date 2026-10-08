using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Npgsql;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;
using Xunit.Abstractions;

namespace Dixels.Postgres.Bookings;

/// <summary>
/// "Busy then" asks, on every preview with colleagues, when they're taken — their own
/// bookings and the meetings they accepted. Like the calendar, that must not grow with how
/// long they've been booking: both halves should seek their (UserId, EndsAt) index to the
/// window, not read a person's whole history.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BusyQueryPlanTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private const int History = 20_000;
    private const int MostRowsRead = 50;

    private static readonly DateTimeOffset Tomorrow = new(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);

    private readonly IBookingRepository _bookingRepository;
    private readonly ITestOutputHelper _output;

    public BusyQueryPlanTests(ITestOutputHelper output)
    {
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _output = output;
    }

    private sealed record Scenario(Guid UserId, Guid OrganiserId, Guid SpaceId);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Busy " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room", spaceType.Id, 8));

        async Task<Guid> NewUserAsync()
        {
            var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return user.Id;
        }

        return new Scenario(await NewUserAsync(), await NewUserAsync(), space.Id);
    });

    /// <summary>
    /// Years of history in straight SQL: the person's own bookings, and as many meetings they
    /// accepted (the organiser's bookings with an accepted guest row), all over by yesterday.
    /// Then tomorrow: one own booking and one accepted meeting.
    /// </summary>
    private static async Task SeedAsync(Scenario s)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();

        async Task RunAsync(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("space", s.SpaceId);
            command.Parameters.AddWithValue("user", s.UserId);
            command.Parameters.AddWithValue("organiser", s.OrganiserId);
            command.Parameters.AddWithValue("yesterday", Tomorrow.AddDays(-2).UtcDateTime);
            command.Parameters.AddWithValue("tomorrow", Tomorrow.UtcDateTime);
            command.Parameters.AddWithValue("count", History);
            await command.ExecuteNonQueryAsync();
        }

        const string insertBooking = """
            INSERT INTO "AppBookings" ("Id", "SpaceId", "UserId", "StartsAt", "EndsAt", "Attendees", "Title", "Status",
                "ResolvedConstraintsJson", "IdempotencyKey", "CancelledByAdmin", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            """;

        // Own history, odd hours.
        await RunAsync(insertBooking + """
            SELECT gen_random_uuid(), @space, @user,
                   @yesterday - make_interval(hours => 2 * n + 1), @yesterday - make_interval(hours => 2 * n),
                   1, '', 'Confirmed', '{}', md5(random()::text), false, '{}', '', now()
            FROM generate_series(1, @count) AS n
            """);

        // The organiser's history, even hours, each with the person as an accepted guest.
        await RunAsync($$"""
            WITH b AS (
              {{insertBooking}}
              SELECT gen_random_uuid(), @space, @organiser,
                     @yesterday - make_interval(hours => 2 * n), @yesterday - make_interval(hours => 2 * n - 1),
                     2, '', 'Confirmed', '{}', md5(random()::text), false, '{}', '', now()
              FROM generate_series(1, @count) AS n
              RETURNING "Id", "EndsAt")
            INSERT INTO "AppBookingAttendees" ("Id", "BookingId", "EndsAt", "UserId", "ResponseStatus")
            SELECT gen_random_uuid(), "Id", "EndsAt", @user, 'Accepted' FROM b
            """);

        // Tomorrow: their own 09–10, and a meeting they accepted 13–14.
        await RunAsync(insertBooking + """
            VALUES (gen_random_uuid(), @space, @user, @tomorrow + interval '9 hours', @tomorrow + interval '10 hours',
                    1, '', 'Confirmed', '{}', md5(random()::text), false, '{}', '', now())
            """);
        await RunAsync($$"""
            WITH b AS (
              {{insertBooking}}
              VALUES (gen_random_uuid(), @space, @organiser, @tomorrow + interval '13 hours', @tomorrow + interval '14 hours',
                      2, '', 'Confirmed', '{}', md5(random()::text), false, '{}', '', now())
              RETURNING "Id", "EndsAt")
            INSERT INTO "AppBookingAttendees" ("Id", "BookingId", "EndsAt", "UserId", "ResponseStatus")
            SELECT gen_random_uuid(), "Id", "EndsAt", @user, 'Accepted' FROM b
            """);

        foreach (var table in new[] { "AppBookings", "AppBookingAttendees" })
        {
            await using var analyze = new NpgsqlCommand($"ANALYZE \"{table}\"", connection);
            await analyze.ExecuteNonQueryAsync();
        }
    }

    private async Task<string> ExplainAsync(NpgsqlCommand captured)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, COSTS OFF) " + captured.CommandText, connection);
        foreach (NpgsqlParameter parameter in captured.Parameters)
        {
            explain.Parameters.Add(parameter.Clone());
        }

        var lines = new List<string>();
        await using var reader = await explain.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        var plan = string.Join(Environment.NewLine, lines);
        _output.WriteLine(captured.CommandText);
        _output.WriteLine(plan);
        return plan;
    }

    /// <summary>Through <paramref name="index"/>, with no table scan, and only a handful of rows handed over.</summary>
    private static void ShouldSeekThrough(string plan, string index, string table)
    {
        plan.ShouldContain(index);
        plan.ShouldNotContain($"Seq Scan on \"{table}\"");
        var indexLine = plan.Split(Environment.NewLine).First(line => line.Contains(index));
        var kept = double.Parse(System.Text.RegularExpressions.Regex.Match(indexLine, @"rows=([\d.]+)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        kept.ShouldBeLessThanOrEqualTo(MostRowsRead);
    }

    [PostgresFact]
    public async Task Who_is_busy_tomorrow_reads_tomorrow_not_the_history()
    {
        var s = await CreateScenarioAsync();
        await SeedAsync(s);

        SqlCapture.Instance.Clear();
        var busy = await WithUnitOfWorkAsync(() =>
            _bookingRepository.GetBusyAsync(new[] { s.UserId }, Tomorrow, Tomorrow.AddDays(1), Array.Empty<Guid>()));

        busy.Select(b => (b.Range.Start, b.IsAcceptedInvite)).OrderBy(x => x.Start).ShouldBe(new[]
        {
            (Tomorrow.AddHours(9), false),
            (Tomorrow.AddHours(13), true),
        });

        var commands = SqlCapture.Instance.Commands.ToList();
        var own = commands.Last(c => c.CommandText.Contains("FROM \"AppBookings\"") && !c.CommandText.Contains("AppBookingAttendees"));
        var accepted = commands.Last(c => c.CommandText.Contains("FROM \"AppBookingAttendees\""));

        ShouldSeekThrough(await ExplainAsync(own), "IX_AppBookings_UserId_EndsAt", "AppBookings");
        ShouldSeekThrough(await ExplainAsync(accepted), "IX_AppBookingAttendees_UserId_EndsAt", "AppBookingAttendees");
    }
}
