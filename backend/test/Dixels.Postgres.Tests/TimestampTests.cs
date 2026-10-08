using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.EntityFrameworkCore;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace Dixels.Postgres;

/// <summary>
/// Npgsql's strict timestamp mode: every instant is a timestamptz, DateTimes come back as
/// Kind Utc and DateTimeOffsets at offset 0, whatever the server's or the machine's time zone.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TimestampTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    // The migration before Use_Timestamptz_For_DateTime: its DateTime columns are still "timestamp".
    private const string LastZonelessMigration = "20261008074051_Add_External_Guest_Indexes";

    private static readonly DateTimeOffset Tomorrow = new(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);

    private Task<(Guid BuildingId, Guid SpaceId)> CreateSpaceAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Times " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room", spaceType.Id, 8));
        return (building.Id, space.Id);
    });

    private async Task<Guid> CreateUserAsync()
    {
        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue());
        return user.Id;
    }

    private Task<Booking> BookAsync(Guid spaceId, Guid userId, DateTimeOffset startsAt) =>
        WithUnitOfWorkAsync(() => GetRequiredService<IBookingRepository>().InsertAsync(new Booking(
            Guid.NewGuid(), spaceId, userId, startsAt, startsAt.AddHours(1), attendees: 1, "Times", "{}", Guid.NewGuid().ToString())));

    [PostgresFact]
    public async Task No_column_is_left_as_a_timestamp_without_a_zone()
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            "SELECT count(*)::int FROM information_schema.columns WHERE table_schema = 'public' AND data_type = 'timestamp without time zone'",
            connection);

        ((int)(await count.ExecuteScalarAsync())!).ShouldBe(0);
    }

    [PostgresFact]
    public async Task A_DateTime_and_a_DateTimeOffset_come_back_as_UTC()
    {
        var (_, spaceId) = await CreateSpaceAsync();
        var startsAt = Tomorrow.AddHours(9);
        var booking = await BookAsync(spaceId, await CreateUserAsync(), startsAt);

        var read = await WithUnitOfWorkAsync(() => GetRequiredService<IBookingRepository>().GetAsync(booking.Id));

        read.StartsAt.ShouldBe(startsAt);
        read.StartsAt.Offset.ShouldBe(TimeSpan.Zero);
        read.CreationTime.Kind.ShouldBe(DateTimeKind.Utc);
        // Postgres keeps microseconds, .NET ticks are 100 ns: equal to the microsecond, not hours off.
        read.CreationTime.ShouldBe(booking.CreationTime, TimeSpan.FromMilliseconds(1));
    }

    [PostgresFact]
    public async Task A_DateTime_with_no_Kind_is_saved_as_UTC_unchanged()
    {
        // ABP's DateTime converter (IClock is UTC) labels it UTC before Npgsql's strict check sees it.
        var unspecified = new DateTime(2026, 10, 8, 9, 30, 0, DateTimeKind.Unspecified);
        var job = new BackgroundJobRecord(Guid.NewGuid())
        {
            JobName = "TimestampTests",
            JobArgs = "{}",
            CreationTime = unspecified,
            NextTryTime = unspecified,
        };
        await WithUnitOfWorkAsync(() => GetRequiredService<IBackgroundJobRepository>().InsertAsync(job));

        var read = await WithUnitOfWorkAsync(() => GetRequiredService<IBackgroundJobRepository>().GetAsync(job.Id));

        read.NextTryTime.Kind.ShouldBe(DateTimeKind.Utc);
        read.NextTryTime.ShouldBe(DateTime.SpecifyKind(unspecified, DateTimeKind.Utc));
    }

    [PostgresFact]
    public async Task A_closure_sent_with_a_local_offset_is_stored_as_the_same_instant_in_UTC()
    {
        var (buildingId, spaceId) = await CreateSpaceAsync();
        var amman = TimeSpan.FromHours(3);
        var startsAt = new DateTimeOffset(Tomorrow.Date.AddHours(12), amman);

        var closure = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AvailabilityOverride, Guid>>().InsertAsync(
            new AvailabilityOverride(Guid.NewGuid(), OverrideScope.Building, buildingId, startsAt, startsAt.AddHours(2),
                OverrideEffect.Closed, ReasonCategory.Maintenance)));
        var read = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AvailabilityOverride, Guid>>().GetAsync(closure.Id));

        read.StartsAt.ShouldBe(startsAt);
        read.StartsAt.Offset.ShouldBe(TimeSpan.Zero);

        // The overlap check is raw SQL (no value converter): a +03:00 window still works, and
        // means the right instant. The booking is 09:00 UTC = 12:00 in Amman.
        await BookAsync(spaceId, await CreateUserAsync(), Tomorrow.AddHours(9));
        var repository = GetRequiredService<IBookingRepository>();
        (await WithUnitOfWorkAsync(() => repository.AnyConfirmedOverlapAsync(spaceId, startsAt, startsAt.AddHours(2)))).ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => repository.AnyConfirmedOverlapAsync(spaceId, startsAt.AddHours(-3), startsAt.AddHours(-2)))).ShouldBeFalse();
    }

    [PostgresFact]
    public async Task The_migration_reads_old_values_as_UTC_on_a_server_in_another_time_zone()
    {
        // A fresh database in the shared container, set to the dev server's zone. EF's own
        // ALTER COLUMN would read every old value as Amman time and move it 3 hours.
        var database = "tz_" + Guid.NewGuid().ToString("N")[..12];
        await using (var admin = new NpgsqlConnection(PostgresFixture.ConnectionString))
        {
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {database}", admin).ExecuteNonQueryAsync();
            await new NpgsqlCommand($"ALTER DATABASE {database} SET timezone = 'Asia/Amman'", admin).ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(PostgresFixture.ConnectionString) { Database = database }.ConnectionString;
        await using var dbContext = new DixelsDbContext(new DbContextOptionsBuilder<DixelsDbContext>().UseNpgsql(connectionString).Options);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(LastZonelessMigration);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        ((string)(await new NpgsqlCommand("SHOW timezone", connection).ExecuteScalarAsync())!).ShouldBe("Asia/Amman");

        var jobId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO "AbpBackgroundJobs" ("Id", "JobName", "JobArgs", "CreationTime", "NextTryTime", "ExtraProperties", "ConcurrencyStamp")
            VALUES (@id, 'Old', '{}', timestamp '2026-10-08 09:00:00', timestamp '2026-10-08 09:05:00', '{}', '')
            """, connection))
        {
            insert.Parameters.AddWithValue("id", jobId);
            await insert.ExecuteNonQueryAsync();
        }

        await migrator.MigrateAsync();

        await using (var read = new NpgsqlCommand($"SELECT \"CreationTime\", \"NextTryTime\" FROM \"AbpBackgroundJobs\" WHERE \"Id\" = '{jobId}'", connection))
        await using (var reader = await read.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).ShouldBeTrue();
            reader.GetDataTypeName(0).ShouldBe("timestamp with time zone");
            reader.GetDateTime(0).ShouldBe(new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc));
            reader.GetDateTime(0).Kind.ShouldBe(DateTimeKind.Utc);
            reader.GetDateTime(1).ShouldBe(new DateTime(2026, 10, 8, 9, 5, 0, DateTimeKind.Utc));
        }

        // And back down: the zoneless column holds the same UTC wall time it started with.
        await migrator.MigrateAsync(LastZonelessMigration);
        var restored = await new NpgsqlCommand(
            $"SELECT \"CreationTime\"::text FROM \"AbpBackgroundJobs\" WHERE \"Id\" = '{jobId}'", connection).ExecuteScalarAsync();
        restored.ShouldBe("2026-10-08 09:00:00");
    }
}
