using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
/// A person's bookings between two times are asked on every calendar load and every booking
/// (the own-clash check), so the cost must not grow with how long they've been booking:
/// Postgres should seek through (UserId, EndsAt) to the few that end after the window starts,
/// not read their whole history.
/// </summary>
[Collection(PostgresCollection.Name)]
public class UserBookingsQueryPlanTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private const string UserIndex = "IX_AppBookings_UserId_EndsAt";

    // Years of history: one booking every two hours, ending yesterday.
    private const int HistoryBookings = 20_000;

    // However long the history, the index should hand over about this week's rows, not thousands.
    private const int MostRowsRead = 50;

    private static readonly DateTimeOffset Tomorrow = new(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);

    private readonly IBookingRepository _bookingRepository;
    private readonly ITestOutputHelper _output;

    public UserBookingsQueryPlanTests(ITestOutputHelper output)
    {
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _output = output;
    }

    private sealed record Scenario(Guid UserId, Guid SpaceId);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG User Plan " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room", spaceType.Id, 8));

        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, space.Id);
    });

    /// <summary>
    /// Straight SQL for the history (twenty thousand through the app would take minutes), then
    /// tomorrow's bookings through the repository: two confirmed, one cancelled by an admin
    /// (still shown on the calendar), one the person cancelled (not shown).
    /// </summary>
    private async Task SeedAsync(Scenario s)
    {
        await using (var connection = new NpgsqlConnection(PostgresFixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO "AppBookings" ("Id", "SpaceId", "UserId", "StartsAt", "EndsAt", "Attendees", "Title", "Status",
                    "ResolvedConstraintsJson", "IdempotencyKey", "CancelledByAdmin", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
                SELECT gen_random_uuid(), @space, @user,
                       @yesterday - make_interval(hours => 2 * n), @yesterday - make_interval(hours => 2 * n - 1),
                       1, '', CASE WHEN n % 10 = 0 THEN 'Cancelled' ELSE 'Confirmed' END,
                       '{}', md5(random()::text), n % 20 = 0, '{}', '', now()
                FROM generate_series(1, @count) AS n
                """, connection);
            insert.Parameters.AddWithValue("space", s.SpaceId);
            insert.Parameters.AddWithValue("user", s.UserId);
            insert.Parameters.AddWithValue("yesterday", Tomorrow.AddDays(-2).UtcDateTime);
            insert.Parameters.AddWithValue("count", HistoryBookings);
            await insert.ExecuteNonQueryAsync();
        }

        await AddBookingAsync(s, 9, 10);
        await AddBookingAsync(s, 13, 14);
        await AddBookingAsync(s, 15, 16, cancelledByAdmin: true);
        await AddBookingAsync(s, 17, 18, cancelledByThem: true);

        await using (var connection = new NpgsqlConnection(PostgresFixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var analyze = new NpgsqlCommand("ANALYZE \"AppBookings\"", connection);
            await analyze.ExecuteNonQueryAsync();
        }
    }

    private Task AddBookingAsync(Scenario s, int startHour, int endHour, bool cancelledByAdmin = false, bool cancelledByThem = false) =>
        WithUnitOfWorkAsync(async () =>
        {
            var booking = new Booking(Guid.NewGuid(), s.SpaceId, s.UserId, Tomorrow.AddHours(startHour), Tomorrow.AddHours(endHour),
                attendees: 1, "Direct", "{}", Guid.NewGuid().ToString());
            if (cancelledByAdmin || cancelledByThem)
            {
                booking.Cancel(s.UserId, DateTimeOffset.UtcNow, reason: null, byAdmin: cancelledByAdmin);
            }

            await _bookingRepository.InsertAsync(booking);
        });

    /// <summary>EXPLAIN of the exact SQL the repository ran (the last bookings query captured), with its parameter values.</summary>
    private async Task<string> ExplainLastBookingsQueryAsync()
    {
        var captured = SqlCapture.Instance.Commands.Last(c => c.CommandText.Contains("FROM \"AppBookings\""));

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

    /// <summary>Through the user index, with no table scan, and the index hands over only a handful of rows.</summary>
    private static void ShouldSeekThroughTheUserIndex(string plan)
    {
        plan.ShouldContain(UserIndex);
        plan.ShouldNotContain("Seq Scan on \"AppBookings\"");
        plan.ShouldMatch(@"Index Cond: .*""EndsAt"" > ");

        // Rows the index handed over: those kept (rows= on its line) plus those a filter dropped.
        var indexLine = plan.Split(Environment.NewLine).First(line => line.Contains(UserIndex));
        var kept = double.Parse(Regex.Match(indexLine, @"rows=([\d.]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        var dropped = Regex.Matches(plan, @"Rows Removed by Filter: (\d+)").Sum(m => int.Parse(m.Groups[1].Value));
        (kept + dropped).ShouldBeLessThanOrEqualTo(MostRowsRead);
    }

    [PostgresFact]
    public async Task The_own_clash_check_reads_this_week_not_the_history()
    {
        var s = await CreateScenarioAsync();
        await SeedAsync(s);

        SqlCapture.Instance.Clear();
        var found = await WithUnitOfWorkAsync(() => _bookingRepository.GetConfirmedForUserAsync(s.UserId, Tomorrow, Tomorrow.AddDays(1)));
        found.Select(b => b.StartsAt).ShouldBe(new[] { Tomorrow.AddHours(9), Tomorrow.AddHours(13) });

        ShouldSeekThroughTheUserIndex(await ExplainLastBookingsQueryAsync());
    }

    [PostgresFact]
    public async Task The_calendar_reads_this_week_not_the_history()
    {
        var s = await CreateScenarioAsync();
        await SeedAsync(s);

        SqlCapture.Instance.Clear();
        var shown = await WithUnitOfWorkAsync(() =>
            _bookingRepository.GetCalendarForUserAsync(s.UserId, Tomorrow, Tomorrow.AddDays(1), DateTimeOffset.UtcNow));
        shown.Select(b => b.StartsAt).ShouldBe(new[] { Tomorrow.AddHours(9), Tomorrow.AddHours(13), Tomorrow.AddHours(15) });

        ShouldSeekThroughTheUserIndex(await ExplainLastBookingsQueryAsync());
    }

    [PostgresFact]
    public async Task A_persons_upcoming_bookings_are_read_without_the_history()
    {
        var s = await CreateScenarioAsync();
        await SeedAsync(s);

        SqlCapture.Instance.Clear();
        var upcoming = await WithUnitOfWorkAsync(() => GetRequiredService<BookingImpactChecker>().FindUpcomingForUserAsync(s.UserId));
        upcoming.Select(b => b.StartsAt).ShouldBe(new[] { Tomorrow.AddHours(9), Tomorrow.AddHours(13) });

        ShouldSeekThroughTheUserIndex(await ExplainLastBookingsQueryAsync());
    }
}
