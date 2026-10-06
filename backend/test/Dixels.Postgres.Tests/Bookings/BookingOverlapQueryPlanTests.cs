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
/// The overlap check is asked on every booking and every availability grid, so it must stay
/// fast however much history a room has: Postgres should answer it from the exclusion
/// constraint's GiST index, not by walking the room's past bookings.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BookingOverlapQueryPlanTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private const string ExclusionIndex = "EX_AppBookings_NoOverlap";

    // Years of history: one booking every two hours, ending yesterday.
    private const int HistoryBookings = 20_000;

    private static readonly DateTimeOffset Tomorrow = new(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);

    private readonly IBookingRepository _bookingRepository;
    private readonly ITestOutputHelper _output;

    public BookingOverlapQueryPlanTests(ITestOutputHelper output)
    {
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _output = output;
    }

    private sealed record Scenario(Guid UserId, Guid[] SpaceIds);

    private Task<Scenario> CreateScenarioAsync(int rooms) => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Plan " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0, ownOverlapPolicy: OwnOverlapPolicy.Warn));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();

        var spaceIds = new List<Guid>();
        for (var i = 0; i < rooms; i++)
        {
            spaceIds.Add((await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(
                new Space(Guid.NewGuid(), floor.Id, "en", "Room " + i, spaceType.Id, 8))).Id);
        }

        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, spaceIds.ToArray());
    });

    /// <summary>Straight SQL: twenty thousand bookings through the app would take minutes.</summary>
    private static async Task SeedHistoryAsync(Scenario s)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();

        foreach (var spaceId in s.SpaceIds)
        {
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO "AppBookings" ("Id", "SpaceId", "UserId", "StartsAt", "EndsAt", "Attendees", "Title", "Status",
                    "ResolvedConstraintsJson", "IdempotencyKey", "CancelledByAdmin", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
                SELECT gen_random_uuid(), @space, @user,
                       @yesterday - make_interval(hours => 2 * n), @yesterday - make_interval(hours => 2 * n - 1),
                       1, '', CASE WHEN n % 10 = 0 THEN 'Cancelled' ELSE 'Confirmed' END,
                       '{}', md5(random()::text), false, '{}', '', now()
                FROM generate_series(1, @count) AS n
                """, connection);
            insert.Parameters.AddWithValue("space", spaceId);
            insert.Parameters.AddWithValue("user", s.UserId);
            insert.Parameters.AddWithValue("yesterday", Tomorrow.AddDays(-2).UtcDateTime);
            insert.Parameters.AddWithValue("count", HistoryBookings);
            await insert.ExecuteNonQueryAsync();
        }

        await using var analyze = new NpgsqlCommand("ANALYZE \"AppBookings\"", connection);
        await analyze.ExecuteNonQueryAsync();
    }

    private Task AddBookingAsync(Scenario s, Guid spaceId, int startHour, int endHour, bool cancelled = false) => WithUnitOfWorkAsync(async () =>
    {
        var booking = new Booking(Guid.NewGuid(), spaceId, s.UserId, Tomorrow.AddHours(startHour), Tomorrow.AddHours(endHour),
            attendees: 1, "Direct", "{}", Guid.NewGuid().ToString());
        if (cancelled)
        {
            booking.Cancel(s.UserId, DateTimeOffset.UtcNow, reason: null, byAdmin: false);
        }

        await _bookingRepository.InsertAsync(booking);
    });

    /// <summary>EXPLAIN of the exact SQL the repository runs, with real parameter values.</summary>
    private async Task<string> ExplainAsync(Guid[] spaceIds, DateTimeOffset start, DateTimeOffset end)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();

        var sql = string.Format(EfCoreBookingRepository.ConfirmedOverlapSql, "@p0", "@p1", "@p2");
        await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, COSTS OFF) " + sql, connection);
        explain.Parameters.AddWithValue("p0", spaceIds);
        explain.Parameters.AddWithValue("p1", start);
        explain.Parameters.AddWithValue("p2", end);

        var lines = new List<string>();
        await using var reader = await explain.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        var plan = string.Join(Environment.NewLine, lines);
        _output.WriteLine(plan);
        return plan;
    }

    [PostgresFact]
    public async Task One_room_with_years_of_history_is_checked_through_the_exclusion_index()
    {
        var s = await CreateScenarioAsync(rooms: 1);
        await SeedHistoryAsync(s);

        var plan = await ExplainAsync(s.SpaceIds, Tomorrow.AddHours(10), Tomorrow.AddHours(11));

        plan.ShouldContain(ExclusionIndex);
        plan.ShouldNotContain("Seq Scan on \"AppBookings\"");
    }

    [PostgresFact]
    public async Task Several_rooms_are_checked_through_the_exclusion_index()
    {
        var s = await CreateScenarioAsync(rooms: 3);
        await SeedHistoryAsync(s);

        var plan = await ExplainAsync(s.SpaceIds, Tomorrow, Tomorrow.AddDays(1));

        // Probing per room or reading the day's range once and matching rooms is the
        // planner's choice; either way it's the index, never the rooms' history.
        plan.ShouldContain(ExclusionIndex);
        plan.ShouldNotContain("Seq Scan on \"AppBookings\"");
    }

    [PostgresFact]
    public async Task The_range_query_finds_the_same_bookings_as_the_overlap_rule()
    {
        var s = await CreateScenarioAsync(rooms: 2);
        var (room, other) = (s.SpaceIds[0], s.SpaceIds[1]);
        await AddBookingAsync(s, room, 9, 10);
        await AddBookingAsync(s, room, 11, 12, cancelled: true);
        await AddBookingAsync(s, other, 10, 11);

        await WithUnitOfWorkAsync(async () =>
        {
            // Overlapping, back-to-back on either side, inside a cancelled slot.
            (await _bookingRepository.AnyConfirmedOverlapAsync(room, Tomorrow.AddHours(9.5), Tomorrow.AddHours(10.5))).ShouldBeTrue();
            (await _bookingRepository.AnyConfirmedOverlapAsync(room, Tomorrow.AddHours(10), Tomorrow.AddHours(11))).ShouldBeFalse();
            (await _bookingRepository.AnyConfirmedOverlapAsync(room, Tomorrow.AddHours(8), Tomorrow.AddHours(9))).ShouldBeFalse();
            (await _bookingRepository.AnyConfirmedOverlapAsync(room, Tomorrow.AddHours(11), Tomorrow.AddHours(12))).ShouldBeFalse();

            var day = await _bookingRepository.GetConfirmedOverlappingAsync(s.SpaceIds, Tomorrow, Tomorrow.AddDays(1));
            day.Select(b => (b.SpaceId, b.StartsAt)).OrderBy(x => x.StartsAt).ShouldBe(new[]
            {
                (room, Tomorrow.AddHours(9)),
                (other, Tomorrow.AddHours(10)),
            });

            // A room named twice still lists each booking once.
            (await _bookingRepository.GetConfirmedOverlappingAsync(new[] { room, room }, Tomorrow, Tomorrow.AddDays(1))).Count.ShouldBe(1);

            // An empty range overlaps nothing, as in TimeRange.Overlaps — and doesn't trip
            // Postgres's "range lower bound must be less than or equal to upper bound".
            (await _bookingRepository.GetConfirmedOverlappingAsync(s.SpaceIds, Tomorrow.AddHours(12), Tomorrow.AddHours(9))).ShouldBeEmpty();
        });
    }
}
