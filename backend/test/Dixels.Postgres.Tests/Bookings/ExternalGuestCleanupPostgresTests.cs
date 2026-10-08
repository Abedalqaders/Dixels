using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Npgsql;
using Shouldly;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;
using Xunit.Abstractions;

namespace Dixels.Postgres.Bookings;

/// <summary>
/// The hourly cleanup of outside guests' details on real Postgres: it finds them through the
/// partial index that holds only outside guests (never the years of colleagues' rows), deletes
/// each batch in one statement, and only one server runs it at a time.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ExternalGuestCleanupPostgresTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private const string ExternalIndex = "IX_AppBookingAttendees_External";

    // Years of history: one booking every two hours, ending yesterday, each with a colleague invited.
    private const int HistoryBookings = 20_000;

    private readonly IBookingRepository _bookingRepository;
    private readonly ITestOutputHelper _output;

    public ExternalGuestCleanupPostgresTests(ITestOutputHelper output)
    {
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _output = output;
    }

    private sealed record Scenario(Guid OwnerId, Guid GuestId, Guid SpaceId);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Cleanup " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
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
    /// Straight SQL (twenty thousand through the app would take minutes): the history, a colleague
    /// on every booking, and outside guests on three bookings that ended ~100 days ago and two
    /// that ended in the last few days.
    /// </summary>
    private static async Task SeedAsync(Scenario s)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var seed = new NpgsqlCommand(
            """
            INSERT INTO "AppBookings" ("Id", "SpaceId", "UserId", "StartsAt", "EndsAt", "Attendees", "Title", "Status",
                "ResolvedConstraintsJson", "IdempotencyKey", "CancelledByAdmin", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            SELECT gen_random_uuid(), @space, @owner,
                   @yesterday - make_interval(hours => 2 * n), @yesterday - make_interval(hours => 2 * n - 1),
                   4, '', 'Confirmed', '{}', md5(random()::text), false, '{}', '', now()
            FROM generate_series(1, @count) AS n;

            INSERT INTO "AppBookingAttendees" ("Id", "BookingId", "EndsAt", "UserId", "ResponseStatus", "IcsUid")
            SELECT gen_random_uuid(), "Id", "EndsAt", @guest, 'Pending', gen_random_uuid()::text || '@dixels'
            FROM "AppBookings" WHERE "UserId" = @owner;

            INSERT INTO "AppBookingAttendees" ("Id", "BookingId", "EndsAt", "Email", "ResponseStatus", "IcsUid")
            SELECT gen_random_uuid(), "Id", "EndsAt", 'guest' || "Id" || '@outside.io', 'Pending', gen_random_uuid()::text || '@dixels'
            FROM (SELECT "Id", "EndsAt" FROM "AppBookings" WHERE "UserId" = @owner AND "EndsAt" < now() - interval '95 days'
                  ORDER BY "EndsAt" DESC LIMIT 3) AS old
            UNION ALL
            SELECT gen_random_uuid(), "Id", "EndsAt", 'guest' || "Id" || '@outside.io', 'Pending', gen_random_uuid()::text || '@dixels'
            FROM (SELECT "Id", "EndsAt" FROM "AppBookings" WHERE "UserId" = @owner
                  ORDER BY "EndsAt" DESC LIMIT 2) AS recent;

            ANALYZE "AppBookings";
            ANALYZE "AppBookingAttendees";
            """, connection);
        seed.Parameters.AddWithValue("space", s.SpaceId);
        seed.Parameters.AddWithValue("owner", s.OwnerId);
        seed.Parameters.AddWithValue("guest", s.GuestId);
        seed.Parameters.AddWithValue("yesterday", DateTime.UtcNow.Date.AddDays(-1));
        seed.Parameters.AddWithValue("count", HistoryBookings);
        await seed.ExecuteNonQueryAsync();
    }

    private static async Task<long> OutsideGuestsOfAsync(Scenario s)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            """
            SELECT count(*) FROM "AppBookingAttendees" a JOIN "AppBookings" b ON b."Id" = a."BookingId"
            WHERE b."UserId" = @owner AND a."UserId" IS NULL
            """, connection);
        count.Parameters.AddWithValue("owner", s.OwnerId);
        return (long)(await count.ExecuteScalarAsync())!;
    }

    /// <summary>EXPLAIN of the exact SQL the repository ran, with its parameter values.</summary>
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
    public async Task The_cleanup_reads_only_outside_guests_and_deletes_in_one_statement()
    {
        var s = await CreateScenarioAsync();
        await SeedAsync(s);
        (await OutsideGuestsOfAsync(s)).ShouldBe(5);

        SqlCapture.Instance.Clear();
        var deleted = await WithUnitOfWorkAsync(() =>
            _bookingRepository.DeleteExpiredExternalGuestsAsync(DateTimeOffset.UtcNow.AddDays(-90), ExternalGuestCleanup.BatchSize));

        deleted.ShouldBe(3);
        (await OutsideGuestsOfAsync(s)).ShouldBe(2);

        // One DELETE for the batch, by id, not one per row.
        var deletes = SqlCapture.Instance.Commands.Where(c => c.CommandText.Contains("DELETE FROM \"AppBookingAttendees\"")).ToList();
        deletes.Count.ShouldBe(1);

        // The candidates come through the outside-guests index: twenty thousand colleague rows are never read.
        var find = SqlCapture.Instance.Commands.Last(c => c.CommandText.Contains("FROM \"AppBookingAttendees\"") && c.CommandText.StartsWith("SELECT"));
        var plan = await ExplainAsync(find);
        plan.ShouldContain(ExternalIndex);
        plan.ShouldNotContain("Seq Scan on \"AppBookingAttendees\"");
        plan.ShouldNotContain("Seq Scan on \"AppBookings\"");
    }

    [PostgresFact]
    public async Task A_server_without_the_lock_skips_the_hour()
    {
        var s = await CreateScenarioAsync();
        await SeedAsync(s);

        // Another server holds the lock: its own connection, so this is a real advisory lock.
        await using (var other = await GetRequiredService<IAbpDistributedLock>().TryAcquireAsync(ExternalGuestCleanup.LockName))
        {
            other.ShouldNotBeNull();

            (await GetRequiredService<ExternalGuestCleanup>().RunAsync()).ShouldBe(0);
            (await OutsideGuestsOfAsync(s)).ShouldBe(5);
        }

        // Released: the next hour cleans.
        (await GetRequiredService<ExternalGuestCleanup>().RunAsync()).ShouldBeGreaterThanOrEqualTo(3);
        (await OutsideGuestsOfAsync(s)).ShouldBe(2);
    }
}
