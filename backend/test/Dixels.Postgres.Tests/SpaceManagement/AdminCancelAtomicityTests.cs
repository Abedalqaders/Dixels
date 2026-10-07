using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Npgsql;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace Dixels.Postgres.SpaceManagement;

/// <summary>
/// A big "cancel what no longer fits" goes in rounds of bulk UPDATEs, all inside the admin's
/// save: if anything fails part-way, nothing is cancelled and the new rules aren't saved.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AdminCancelAtomicityTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private Task<(Guid BuildingId, Guid SpaceId)> CreateBuildingAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Atomic " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0)),
            maxDurationMinutes: 180, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room", spaceType.Id, 8));
        return (building.Id, space.Id);
    });

    /// <summary>
    /// 1,200 one-minute bookings from 16:00 on, straight into the database: the 900 up to 07:00
    /// the next morning fall outside 07:00–16:00, the 300 after it still fit.
    /// </summary>
    private async Task SeedAsync(Guid spaceId)
    {
        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue());

        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO "AppBookings" ("Id", "SpaceId", "UserId", "StartsAt", "EndsAt", "Attendees", "Title", "Status",
                "ResolvedConstraintsJson", "IdempotencyKey", "CancelledByAdmin", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            SELECT gen_random_uuid(), @space, @user, @from + make_interval(mins => n), @from + make_interval(mins => n + 1),
                   2, 'Slot', 'Confirmed', '{}', md5(random()::text), false, '{}', '', now()
            FROM generate_series(0, 1199) AS n
            """, connection);
        insert.Parameters.AddWithValue("space", spaceId);
        insert.Parameters.AddWithValue("user", user.Id);
        insert.Parameters.AddWithValue("from", Tomorrow.AddHours(16));
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(Guid spaceId, string status)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            "SELECT count(*)::int FROM \"AppBookings\" WHERE \"SpaceId\" = @space AND \"Status\" = @status", connection);
        count.Parameters.AddWithValue("space", spaceId);
        count.Parameters.AddWithValue("status", status);
        return (int)(await count.ExecuteScalarAsync())!;
    }

    private async Task<UpdateBuildingConstraintsDto> CloseAtFourAsync(Guid buildingId)
    {
        var current = await GetRequiredService<IBuildingsAppService>().GetAsync(buildingId);
        return new UpdateBuildingConstraintsDto
        {
            Days = current.Days,
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "07:00", Close = "16:00" },
            MaxDurationMinutes = current.MaxDurationMinutes,
            MaxHorizonDays = current.MaxHorizonDays,
            MinLeadMinutes = current.MinLeadMinutes,
            OwnOverlapPolicy = current.OwnOverlapPolicy,
            ConcurrencyStamp = current.ConcurrencyStamp,
            CancelAffectedBookings = true,
        };
    }

    private IDisposable ActAsAdmin() =>
        GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
        })));

    [PostgresFact]
    public async Task A_failure_part_way_through_the_rounds_cancels_nothing_and_saves_nothing()
    {
        var (buildingId, spaceId) = await CreateBuildingAsync();
        await SeedAsync(spaceId);
        var input = await CloseAtFourAsync(buildingId);

        // The second round's announcement fails (rounds of 500 and 400): by then both UPDATEs have run.
        var rounds = 0;
        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingsCancelledEvent>(_ =>
                   ++rounds == 2 ? throw new InvalidOperationException("Something failed part-way.") : Task.CompletedTask))
        using (ActAsAdmin())
        {
            await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(
                new AbpUnitOfWorkOptions(isTransactional: true),
                () => GetRequiredService<IBuildingsAppService>().UpdateConstraintsAsync(buildingId, input)));
        }

        rounds.ShouldBe(2);
        (await CountAsync(spaceId, "Cancelled")).ShouldBe(0);
        (await CountAsync(spaceId, "Confirmed")).ShouldBe(1200);
        (await GetRequiredService<IBuildingsAppService>().GetAsync(buildingId)).Hours.Close.ShouldBe("20:00");
    }

    [PostgresFact]
    public async Task Without_a_failure_every_round_is_kept()
    {
        var (buildingId, spaceId) = await CreateBuildingAsync();
        await SeedAsync(spaceId);
        var input = await CloseAtFourAsync(buildingId);

        using (ActAsAdmin())
        {
            (await WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true),
                () => GetRequiredService<IBuildingsAppService>().UpdateConstraintsAsync(buildingId, input))).CancelledBookings.ShouldBe(900);
        }

        (await CountAsync(spaceId, "Cancelled")).ShouldBe(900);
        (await CountAsync(spaceId, "Confirmed")).ShouldBe(300);
        (await GetRequiredService<IBuildingsAppService>().GetAsync(buildingId)).Hours.Close.ShouldBe("16:00");
    }
}
