using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Npgsql;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;
using Xunit.Abstractions;

namespace Dixels.Postgres.SpaceManagement;

/// <summary>
/// What each admin save costs on a big building: 10 floors of 50 rooms with 60 days of
/// bookings ahead (90,000). Each save is run once and reported (SQL commands sent, time), in
/// the order an admin might do them: the "keep" saves first, then the cancelling ones.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AdminSaveCostTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private const int Floors = 10;
    private const int RoomsPerFloor = 50;
    private const int Days = 60;

    // Three bookings a room a day: 09–10, 11–12 and 14–15 (UTC, the building's clock).
    private static readonly int[] StartHours = { 9, 11, 14 };

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly ITestOutputHelper _output;

    public AdminSaveCostTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed record BigBuilding(Guid BuildingId, Guid UserId, List<Guid> FloorIds, List<Guid> SpaceIds);

    private Task<BigBuilding> CreateBigBuildingAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Saves " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0)),
            maxDurationMinutes: 180, maxHorizonDays: 90, minLeadMinutes: 0));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var floorIds = new List<Guid>();
        var spaceIds = new List<Guid>();
        for (var f = 0; f < Floors; f++)
        {
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", $"Level {f + 1}", f + 1));
            floorIds.Add(floor.Id);
            for (var r = 0; r < RoomsPerFloor; r++)
            {
                spaceIds.Add((await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(
                    new Space(Guid.NewGuid(), floor.Id, "en", $"Room {f + 1}.{r + 1}", spaceType.Id, 8))).Id);
            }
        }

        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        user.SetBuildingId(building.Id);
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
        return new BigBuilding(building.Id, user.Id, floorIds, spaceIds);
    });

    /// <summary>Straight SQL: 90,000 bookings through the app would take minutes.</summary>
    private static async Task SeedBookingsAsync(BigBuilding b)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO "AppBookings" ("Id", "SpaceId", "UserId", "StartsAt", "EndsAt", "Attendees", "Title", "Status",
                "ResolvedConstraintsJson", "IdempotencyKey", "CancelledByAdmin", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            SELECT gen_random_uuid(), s."Id", @user,
                   @tomorrow + make_interval(days => d, hours => h), @tomorrow + make_interval(days => d, hours => h + 1),
                   2, 'Planning', 'Confirmed', '{}', md5(random()::text), false, '{}', '', now()
            FROM "AppSpaces" s
            JOIN "AppFloors" f ON f."Id" = s."FloorId" AND f."BuildingId" = @building
            CROSS JOIN generate_series(0, @days - 1) AS d
            CROSS JOIN unnest(@hours) AS h
            """, connection);
        insert.Parameters.AddWithValue("user", b.UserId);
        insert.Parameters.AddWithValue("building", b.BuildingId);
        insert.Parameters.AddWithValue("tomorrow", Tomorrow);
        insert.Parameters.AddWithValue("days", Days);
        insert.Parameters.AddWithValue("hours", StartHours);
        await insert.ExecuteNonQueryAsync();

        await using var analyze = new NpgsqlCommand("ANALYZE \"AppBookings\"", connection);
        await analyze.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountConfirmedAsync(BigBuilding b)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            "SELECT count(*)::int FROM \"AppBookings\" WHERE \"Status\" = 'Confirmed' AND \"SpaceId\" = ANY(@spaces)", connection);
        count.Parameters.AddWithValue("spaces", b.SpaceIds.ToArray());
        return (int)(await count.ExecuteScalarAsync())!;
    }

    private IDisposable ActAsAdmin() =>
        GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
        })));

    /// <summary>Runs one save (in its own unit of work, like a request) and reports its SQL commands and time.</summary>
    private async Task<T> MeasureAsync<T>(string name, Func<Task<T>> save)
    {
        SqlCapture.Instance.Clear();
        var watch = Stopwatch.StartNew();
        var result = await WithUnitOfWorkAsync(save);
        watch.Stop();
        var commands = SqlCapture.Instance.Commands;
        _output.WriteLine($"{name}: {commands.Count} SQL commands, {watch.ElapsedMilliseconds} ms");
        foreach (var group in commands.GroupBy(c => c.CommandText.Split('\n')[0].Trim()))
        {
            _output.WriteLine($"  {group.Count()} x {(group.Key.Length > 140 ? group.Key[..140] + "…" : group.Key)}");
        }

        return result;
    }

    private async Task<UpdateBuildingConstraintsDto> BuildingRulesAsync(Guid id, string close, bool cancel)
    {
        var current = await GetRequiredService<IBuildingsAppService>().GetAsync(id);
        return new UpdateBuildingConstraintsDto
        {
            Days = current.Days,
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "07:00", Close = close },
            MaxDurationMinutes = current.MaxDurationMinutes,
            MaxHorizonDays = current.MaxHorizonDays,
            MinLeadMinutes = current.MinLeadMinutes,
            OwnOverlapPolicy = current.OwnOverlapPolicy,
            ConcurrencyStamp = current.ConcurrencyStamp,
            CancelAffectedBookings = cancel,
        };
    }

    private async Task<UpdateFloorConstraintsDto> FloorRulesAsync(Guid id, string close, bool cancel)
    {
        var current = await GetRequiredService<IFloorsAppService>().GetAsync(id);
        return new UpdateFloorConstraintsDto
        {
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "07:00", Close = close },
            ConcurrencyStamp = current.ConcurrencyStamp,
            CancelAffectedBookings = cancel,
        };
    }

    private static CreateAvailabilityOverrideDto Closure(OverrideScope scope, Guid scopeId, int day, int hour, bool cancel) => new()
    {
        Scope = scope,
        ScopeId = scopeId,
        StartsAt = new DateTimeOffset(Tomorrow.AddDays(day).AddHours(hour), TimeSpan.Zero),
        EndsAt = new DateTimeOffset(Tomorrow.AddDays(day).AddHours(hour + 1), TimeSpan.Zero),
        Effect = OverrideEffect.Closed,
        ReasonCategory = ReasonCategory.Maintenance,
        ReasonDetail = "Cost test",
        CancelAffectedBookings = cancel,
    };

    [PostgresFact]
    public async Task What_each_admin_save_costs_on_a_big_building()
    {
        var b = await CreateBigBuildingAsync();
        var buildings = GetRequiredService<IBuildingsAppService>();
        var floors = GetRequiredService<IFloorsAppService>();
        var spaces = GetRequiredService<ISpacesAppService>();
        var closures = GetRequiredService<IAvailabilityOverridesAppService>();
        using var _ = ActAsAdmin();

        // Before any bookings: a time zone change only has to count them.
        var details = await buildings.GetAsync(b.BuildingId);
        await MeasureAsync("Time zone change (count upcoming)", () => buildings.UpdateAsync(b.BuildingId, new UpdateBuildingDto
        {
            Names = details.Names,
            BuildingNumber = details.BuildingNumber,
            Timezone = "Etc/UTC",
        }));

        await SeedBookingsAsync(b);
        var total = await CountConfirmedAsync(b);

        // Keeping what no longer fits: nothing is cancelled.
        await MeasureAsync("Building rules, keep", async () => await buildings.UpdateConstraintsAsync(b.BuildingId, await BuildingRulesAsync(b.BuildingId, "14:00", cancel: false)));
        await MeasureAsync("Floor rules, keep", async () => await floors.UpdateConstraintsAsync(b.FloorIds[0], await FloorRulesAsync(b.FloorIds[0], "14:00", cancel: false)));
        await MeasureAsync("Closure on the building, keep", () => closures.CreateAsync(Closure(OverrideScope.Building, b.BuildingId, day: 0, hour: 9, cancel: false)));
        (await CountConfirmedAsync(b)).ShouldBe(total);

        // Cancelling: one room's capacity, a floor closure, then the whole building's hours.
        var room = await spaces.GetAsync(b.SpaceIds[^1]);
        await MeasureAsync("Room capacity 8 -> 1, cancel", () => spaces.UpdateAsync(room.Id, new UpdateSpaceDto
        {
            Names = room.Names,
            SpaceTypeId = room.SpaceTypeId,
            Capacity = 1,
            CancelAffectedBookings = true,
        }));
        var afterCapacity = await CountConfirmedAsync(b);
        _output.WriteLine($"  cancelled {total - afterCapacity}");

        await MeasureAsync("Closure on a floor, cancel", () => closures.CreateAsync(Closure(OverrideScope.Floor, b.FloorIds[1], day: 1, hour: 9, cancel: true)));
        var afterClosure = await CountConfirmedAsync(b);
        _output.WriteLine($"  cancelled {afterCapacity - afterClosure}");

        await MeasureAsync("Floor rules, cancel", async () => await floors.UpdateConstraintsAsync(b.FloorIds[2], await FloorRulesAsync(b.FloorIds[2], "12:00", cancel: true)));
        var afterFloor = await CountConfirmedAsync(b);
        _output.WriteLine($"  cancelled {afterClosure - afterFloor}");

        var saved = await MeasureAsync("Building rules, cancel", async () => await buildings.UpdateConstraintsAsync(b.BuildingId, await BuildingRulesAsync(b.BuildingId, "14:00", cancel: true)));
        var afterBuilding = await CountConfirmedAsync(b);
        _output.WriteLine($"  cancelled {afterFloor - afterBuilding} (save reported {saved.CancelledBookings})");

        // The person's side: what moving them would release, and a permission-filtered user list.
        await MeasureAsync("Reassign preview", () => GetRequiredService<IUsersAppService>().GetReassignImpactAsync(b.UserId));
        var input = new GetIdentityUsersInput();
        input.ExtraProperties[DixelsUserConsts.PermissionFilterKey] = "Dixels.Bookings";
        await MeasureAsync("User list filtered by permission", () => GetRequiredService<IIdentityUserAppService>().GetListAsync(input));
    }
}
