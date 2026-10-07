using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Npgsql;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;
using Xunit.Abstractions;

namespace Dixels.Postgres.Bookings;

/// <summary>
/// An admin's impact preview on a big building: 500 rooms with 60 days of bookings ahead
/// (90,000). The delete preview counts them in SQL and reads one page; the rule-change
/// preview still checks every one, but reads only a few columns and describes one page.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BookingImpactPreviewSizeTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private const int Floors = 10;
    private const int RoomsPerFloor = 50;
    private const int Days = 60;

    // Three bookings a room a day: 09–10, 11–12 and 14–15 (UTC, the building's clock).
    private static readonly int[] StartHours = { 9, 11, 14 };
    private const int Bookings = Floors * RoomsPerFloor * Days * 3;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly ITestOutputHelper _output;

    public BookingImpactPreviewSizeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private Task<(Guid BuildingId, Guid UserId)> CreateBigBuildingAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Big " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0)),
            maxDurationMinutes: 180, maxHorizonDays: 90, minLeadMinutes: 0));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        for (var f = 0; f < Floors; f++)
        {
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", $"Level {f + 1}", f + 1));
            for (var r = 0; r < RoomsPerFloor; r++)
            {
                await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(
                    new Space(Guid.NewGuid(), floor.Id, "en", $"Room {f + 1}.{r + 1}", spaceType.Id, 8));
            }
        }

        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
        return (building.Id, user.Id);
    });

    /// <summary>Straight SQL: 90,000 bookings through the app would take minutes.</summary>
    private static async Task SeedBookingsAsync(Guid buildingId, Guid userId)
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
        insert.Parameters.AddWithValue("user", userId);
        insert.Parameters.AddWithValue("building", buildingId);
        insert.Parameters.AddWithValue("tomorrow", Tomorrow);
        insert.Parameters.AddWithValue("days", Days);
        insert.Parameters.AddWithValue("hours", StartHours);
        await insert.ExecuteNonQueryAsync();

        await using var analyze = new NpgsqlCommand("ANALYZE \"AppBookings\"", connection);
        await analyze.ExecuteNonQueryAsync();
    }

    private IDisposable ActAsAdmin() =>
        GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
        })));

    /// <summary>Runs a preview (warmed up once), and reports how long it took, how many SQL commands it sent and how many rows it returned.</summary>
    private async Task<ReservationImpactDto> MeasureAsync(string name, Func<Task<ReservationImpactDto>> preview)
    {
        await preview();
        SqlCapture.Instance.Clear();
        var watch = Stopwatch.StartNew();
        var impact = await preview();
        watch.Stop();
        var commands = SqlCapture.Instance.Commands;
        _output.WriteLine($"{name}: {watch.ElapsedMilliseconds} ms, {commands.Count} SQL commands, Count = {impact.Count}, Items returned = {impact.Items.Count}");
        foreach (var command in commands)
        {
            _output.WriteLine("  " + command.CommandText.Split('\n')[0]);
        }

        return impact;
    }

    [PostgresFact]
    public async Task A_big_building_preview_reads_one_page()
    {
        var (buildingId, userId) = await CreateBigBuildingAsync();
        await SeedBookingsAsync(buildingId, userId);
        var buildings = GetRequiredService<IBuildingsAppService>();
        using var _ = ActAsAdmin();

        var delete = await MeasureAsync("Delete preview", () => buildings.GetDeleteImpactAsync(buildingId));
        var commands = SqlCapture.Instance.Commands;

        // Closing at 14:00 leaves every 14–15 booking outside: a third of them.
        var current = await buildings.GetAsync(buildingId);
        var narrower = new UpdateBuildingConstraintsDto
        {
            Days = current.Days,
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "07:00", Close = "14:00" },
            MaxDurationMinutes = current.MaxDurationMinutes,
            MaxHorizonDays = current.MaxHorizonDays,
            MinLeadMinutes = current.MinLeadMinutes,
            OwnOverlapPolicy = current.OwnOverlapPolicy,
            ConcurrencyStamp = current.ConcurrencyStamp,
        };
        var rules = await MeasureAsync("Rule-change preview", () => buildings.GetConstraintsImpactAsync(buildingId, narrower));

        delete.Count.ShouldBe(Bookings);
        delete.Items.Count.ShouldBe(ReservationImpactPreview.PageSize);
        // The rooms are found inside the bookings query: the building's rooms are never read
        // as a list (by floor), only the page's rooms, by id, for their names.
        commands.ShouldNotContain(c => c.CommandText.Contains("FROM \"AppSpaces\"") && c.CommandText.Contains("\"FloorId\" = ANY"));
        rules.Count.ShouldBe(Bookings / 3);
        rules.Items.Count.ShouldBe(ReservationImpactPreview.PageSize);
    }
}
