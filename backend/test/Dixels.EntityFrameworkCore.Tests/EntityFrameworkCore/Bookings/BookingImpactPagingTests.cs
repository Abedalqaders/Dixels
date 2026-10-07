using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// The impact previews an admin reads before a change are shown a page at a time ("Show
/// more"): the count is always everything affected, the list is the soonest 50 from where
/// the admin has read to — and a save still acts on all of them, not on what was shown.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingImpactPagingTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private const int Page = ReservationImpactPreview.PageSize;

    private readonly IBuildingsAppService _buildings;
    private readonly IFloorsAppService _floors;
    private readonly ISpacesAppService _spaces;
    private readonly IUsersAppService _users;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);
    private static readonly Guid Admin = Guid.NewGuid();

    public BookingImpactPagingTests()
    {
        _buildings = GetRequiredService<IBuildingsAppService>();
        _floors = GetRequiredService<IFloorsAppService>();
        _spaces = GetRequiredService<ISpacesAppService>();
        _users = GetRequiredService<IUsersAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Scenario(Guid UserId, Building Building, Floor Floor, Space Space, Space OtherFloorSpace);

    /// <summary>A UTC building open 07:00–20:00 with two floors of one room each, and an employee assigned to it.</summary>
    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "Paging HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0)),
            maxDurationMinutes: 180, maxHorizonDays: 30, minLeadMinutes: 0));
        var floors = GetRequiredService<IRepository<Floor, Guid>>();
        var floor = await floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var upstairs = await floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 2", 2));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var spaces = GetRequiredService<IRepository<Space, Guid>>();
        var space = await spaces.InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, 8));
        var other = await spaces.InsertAsync(new Space(Guid.NewGuid(), upstairs.Id, "en", "Room 2", spaceType.Id, 8));

        var user = new IdentityUser(Guid.NewGuid(), "emp" + Guid.NewGuid().ToString("N")[..8], $"{Guid.NewGuid():N}@test.io");
        user.SetBuildingId(building.Id);
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, building, floor, space, other);
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    /// <summary>One-minute bookings back to back from <paramref name="firstUtc"/>, straight into the database.</summary>
    private Task<List<Guid>> AddBookingsAsync(Scenario s, Guid spaceId, DateTime firstUtc, int count) => WithUnitOfWorkAsync(async () =>
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var start = new DateTimeOffset(firstUtc, TimeSpan.Zero).AddMinutes(i);
            var booking = await _bookingRepository.InsertAsync(new Booking(
                Guid.NewGuid(), spaceId, s.UserId, start, start.AddMinutes(1),
                attendees: 2, title: "Slot", resolvedConstraintsJson: "{}", idempotencyKey: Guid.NewGuid().ToString()));
            ids.Add(booking.Id);
        }

        return ids;
    });

    private async Task<UpdateBuildingConstraintsDto> BuildingHoursAsync(Scenario s, string open, string close, bool cancel = false)
    {
        var current = await _buildings.GetAsync(s.Building.Id);
        return new UpdateBuildingConstraintsDto
        {
            Days = current.Days,
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = open, Close = close },
            MaxDurationMinutes = current.MaxDurationMinutes,
            MaxHorizonDays = current.MaxHorizonDays,
            MinLeadMinutes = current.MinLeadMinutes,
            OwnOverlapPolicy = current.OwnOverlapPolicy,
            ConcurrencyStamp = current.ConcurrencyStamp,
            CancelAffectedBookings = cancel,
        };
    }

    /// <summary>Reads every page of a preview, as "Show more" would.</summary>
    private static async Task<List<ReservationImpactDto>> AllPagesAsync(Func<int, Task<ReservationImpactDto>> page)
    {
        var pages = new List<ReservationImpactDto> { await page(0) };
        while (pages.Sum(p => p.Items.Count) < pages[0].Count)
        {
            pages.Add(await page(pages.Sum(p => p.Items.Count)));
        }

        return pages;
    }

    [Fact]
    public async Task A_delete_preview_counts_everything_and_lists_fifty_at_a_time_soonest_first()
    {
        var s = await CreateScenarioAsync();
        var ids = await AddBookingsAsync(s, s.Space.Id, Tomorrow.AddHours(9), 120);

        using var _ = ActAs(Admin);
        var pages = await AllPagesAsync(skip => _spaces.GetDeleteImpactAsync(s.Space.Id, skip));

        pages.Select(p => p.Count).ShouldAllBe(c => c == 120);
        pages.Select(p => p.Items.Count).ShouldBe(new[] { Page, Page, 20 });
        // Soonest first, each one once: the pages join up with nothing repeated or missed.
        pages.SelectMany(p => p.Items).Select(i => i.Id).ShouldBe(ids);
        pages[0].Items[0].LocalStart.ShouldBe(Tomorrow.AddHours(9));
        pages[0].Items[0].PlaceName.ShouldBe("Room 1");
        pages[0].Items[0].PlaceDetail.ShouldBe("Level 1");
    }

    [Fact]
    public async Task A_building_or_floor_delete_preview_finds_the_rooms_itself()
    {
        var s = await CreateScenarioAsync();
        await AddBookingsAsync(s, s.Space.Id, Tomorrow.AddHours(9), 60);
        await AddBookingsAsync(s, s.OtherFloorSpace.Id, Tomorrow.AddHours(9), 30);
        var elsewhere = await CreateScenarioAsync();
        await AddBookingsAsync(elsewhere, elsewhere.Space.Id, Tomorrow.AddHours(9), 10);

        using var _ = ActAs(Admin);
        var building = await _buildings.GetDeleteImpactAsync(s.Building.Id);
        var floor = await _floors.GetDeleteImpactAsync(s.Floor.Id);

        building.Count.ShouldBe(90);
        building.Items.Count.ShouldBe(Page);
        // Both floors, interleaved by time.
        building.Items.Select(i => i.PlaceDetail).Distinct().OrderBy(n => n).ShouldBe(new[] { "Level 1", "Level 2" });
        floor.Count.ShouldBe(60);
        floor.Items.ShouldAllBe(i => i.PlaceDetail == "Level 1");
    }

    [Fact]
    public async Task A_rule_change_preview_counts_only_the_bookings_that_no_longer_fit()
    {
        var s = await CreateScenarioAsync();
        var inside = await AddBookingsAsync(s, s.Space.Id, Tomorrow.AddHours(10), 50);
        var outside = await AddBookingsAsync(s, s.Space.Id, Tomorrow.AddHours(17), 70);

        using var _ = ActAs(Admin);
        var input = await BuildingHoursAsync(s, "09:00", "17:00");
        var pages = await AllPagesAsync(skip => _buildings.GetConstraintsImpactAsync(s.Building.Id, input, skip));

        pages[0].Count.ShouldBe(70);
        pages.Select(p => p.Items.Count).ShouldBe(new[] { Page, 20 });
        pages.SelectMany(p => p.Items).Select(i => i.Id).ShouldBe(outside);
        pages.SelectMany(p => p.Items).ShouldNotContain(i => inside.Contains(i.Id));
        pages[0].Items[0].Reasons.ShouldBe(new[] { "Open 09:00–17:00 only" });
    }

    [Fact]
    public async Task Saving_with_cancel_cancels_every_booking_that_no_longer_fits_not_just_the_first_page()
    {
        var s = await CreateScenarioAsync();
        var inside = await AddBookingsAsync(s, s.Space.Id, Tomorrow.AddHours(10), 50);
        var outside = await AddBookingsAsync(s, s.Space.Id, Tomorrow.AddHours(17), 70);

        using (ActAs(Admin))
        {
            var saved = await _buildings.UpdateConstraintsAsync(s.Building.Id, await BuildingHoursAsync(s, "09:00", "17:00", cancel: true));
            saved.CancelledBookings.ShouldBe(70);
        }

        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetListAsync(b => b.SpaceId == s.Space.Id));
        stored.Where(b => outside.Contains(b.Id)).ShouldAllBe(b => b.Status == BookingStatus.Cancelled && b.CancelReason == "Rules changed: Open 09:00–17:00 only");
        stored.Where(b => inside.Contains(b.Id)).ShouldAllBe(b => b.Status == BookingStatus.Confirmed);
    }

    [Fact]
    public async Task A_reassign_preview_counts_the_persons_bookings_in_the_building_and_pages_them()
    {
        var s = await CreateScenarioAsync();
        var mine = await AddBookingsAsync(s, s.Space.Id, Tomorrow.AddHours(9), 40);
        mine.AddRange(await AddBookingsAsync(s, s.OtherFloorSpace.Id, Tomorrow.AddHours(12), 30));
        var elsewhere = await CreateScenarioAsync();
        // Same person, another building: a move away from this one doesn't touch it.
        await AddBookingsAsync(s, elsewhere.Space.Id, Tomorrow.AddHours(9), 5);

        using var _ = ActAs(Admin);
        var pages = await AllPagesAsync(skip => _users.GetReassignImpactAsync(s.UserId, skip));

        pages[0].Count.ShouldBe(70);
        pages.Select(p => p.Items.Count).ShouldBe(new[] { Page, 20 });
        pages.SelectMany(p => p.Items).Select(i => i.Id).ShouldBe(mine);
    }
}
