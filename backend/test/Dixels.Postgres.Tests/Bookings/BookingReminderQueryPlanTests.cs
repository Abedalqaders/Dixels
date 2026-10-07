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
/// The reminder query runs every minute, so it must stay cheap however much history there is:
/// Postgres should find the window's bookings through an index, never by reading the table.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BookingReminderQueryPlanTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    // History from before reminders existed (confirmed, never reminded: still in the index),
    // one booking every two hours, ending yesterday.
    private const int HistoryBookings = 20_000;

    // Due now: two-second bookings three seconds apart, starting a minute from now.
    private const int DueBookings = 450;

    private readonly IBookingRepository _bookingRepository;
    private readonly ITestOutputHelper _output;

    public BookingReminderQueryPlanTests(ITestOutputHelper output)
    {
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _output = output;
    }

    private sealed record Scenario(Guid UserId, Guid SpaceId);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Reminder Plan " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room", spaceType.Id, 8));

        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, space.Id);
    });

    /// <summary>Straight SQL (history, then the due ones): twenty thousand bookings through the app would take minutes.</summary>
    private static async Task SeedAsync(Scenario s, DateTimeOffset now)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();

        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO "AppBookings" ("Id", "SpaceId", "UserId", "StartsAt", "EndsAt", "Attendees", "Title", "Status",
                "ResolvedConstraintsJson", "IdempotencyKey", "CancelledByAdmin", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            SELECT gen_random_uuid(), @space, @user,
                   @yesterday - make_interval(hours => 2 * n), @yesterday - make_interval(hours => 2 * n - 1),
                   1, '', 'Confirmed', '{}', md5(random()::text), false, '{}', '', @made
            FROM generate_series(1, @history) AS n;
            INSERT INTO "AppBookings" ("Id", "SpaceId", "UserId", "StartsAt", "EndsAt", "Attendees", "Title", "Status",
                "ResolvedConstraintsJson", "IdempotencyKey", "CancelledByAdmin", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            SELECT gen_random_uuid(), @space, @user,
                   @soon + make_interval(secs => 3 * n), @soon + make_interval(secs => 3 * n + 2),
                   1, '', 'Confirmed', '{}', md5(random()::text), false, '{}', '', @made
            FROM generate_series(1, @due) AS n
            """, connection);
        insert.Parameters.AddWithValue("space", s.SpaceId);
        insert.Parameters.AddWithValue("user", s.UserId);
        insert.Parameters.AddWithValue("yesterday", now.AddDays(-1).UtcDateTime);
        insert.Parameters.AddWithValue("soon", now.AddMinutes(1).UtcDateTime);
        insert.Parameters.AddWithValue("made", now.AddDays(-2).UtcDateTime);
        insert.Parameters.AddWithValue("history", HistoryBookings);
        insert.Parameters.AddWithValue("due", DueBookings);
        await insert.ExecuteNonQueryAsync();

        await using var analyze = new NpgsqlCommand("ANALYZE \"AppBookings\"", connection);
        await analyze.ExecuteNonQueryAsync();
    }

    /// <summary>EXPLAIN of the exact SQL the repository ran (captured), with its parameter values.</summary>
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

    [PostgresFact]
    public async Task The_next_batch_is_found_through_an_index()
    {
        var s = await CreateScenarioAsync();
        var now = DateTimeOffset.UtcNow;
        await SeedAsync(s, now);

        SqlCapture.Instance.Clear();
        var batch = await WithUnitOfWorkAsync(() => _bookingRepository.GetDueForReminderAsync(
            now, now.AddMinutes(30), BookingReminders.BatchSize, new[] { Guid.NewGuid() }));
        batch.Count.ShouldBe(BookingReminders.BatchSize);

        var query = SqlCapture.Instance.Commands.Last(c => c.CommandText.Contains("\"ReminderSentAt\" IS NULL"));
        var plan = await ExplainAsync(query);

        // Which index is the planner's choice: the partial reminder index (IX_AppBookings_ReminderDue)
        // or, with few rooms, room by room on IX_AppBookings_SpaceId_StartsAt_EndsAt. Either way
        // the time window is an index condition, not a filter over the history.
        plan.ShouldNotContain("Seq Scan on \"AppBookings\"");
        plan.ShouldMatch(@"Index Cond: .*""StartsAt"" > ");
    }
}
