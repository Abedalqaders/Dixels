using System;
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
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;
using static Dixels.TestNames;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// An admin tightening a rule, adding a closure or removing a room: which upcoming bookings
/// that leaves behind, and what happens to them (kept by default, or cancelled as an admin
/// cancel that the employee still sees, with why).
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingImpactTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookings;
    private readonly IAvailabilityAppService _availability;
    private readonly IBuildingsAppService _buildings;
    private readonly IFloorsAppService _floors;
    private readonly ISpacesAppService _spaces;
    private readonly IAvailabilityOverridesAppService _closures;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);
    private static readonly Guid Admin = Guid.NewGuid();

    public BookingImpactTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _availability = GetRequiredService<IAvailabilityAppService>();
        _buildings = GetRequiredService<IBuildingsAppService>();
        _floors = GetRequiredService<IFloorsAppService>();
        _spaces = GetRequiredService<ISpacesAppService>();
        _closures = GetRequiredService<IAvailabilityOverridesAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Scenario(Guid UserId, Building Building, Floor Floor, Space Space);

    /// <summary>A UTC building open every day 07:00–20:00 (max 3h), one 8-seat room, and Jordan Reed assigned to it.</summary>
    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "Impact HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0)),
            maxDurationMinutes: 180, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, 8));

        var user = new IdentityUser(Guid.NewGuid(), "emp" + Guid.NewGuid().ToString("N")[..8], $"{Guid.NewGuid():N}@test.io")
        {
            Name = "Jordan",
            Surname = "Reed",
        };
        user.SetBuildingId(building.Id);
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, building, floor, space);
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private async Task<BookingDto> BookAsync(Scenario s, int startHour, int endHour, int attendees = 2, Guid? spaceId = null)
    {
        using var _ = ActAs(s.UserId);
        return await _bookings.CreateAsync(new CreateBookingDto
        {
            SpaceId = spaceId ?? s.Space.Id,
            LocalStart = Tomorrow.AddHours(startHour),
            LocalEnd = Tomorrow.AddHours(endHour),
            Attendees = attendees,
            Title = "Planning",
            IdempotencyKey = Guid.NewGuid().ToString(),
        });
    }

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

    private Task<Booking> StoredAsync(Guid id) => WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(id));

    [Fact]
    public async Task Narrowing_the_building_hours_lists_the_bookings_left_outside_them_and_saves_nothing()
    {
        var s = await CreateScenarioAsync();
        var late = await BookAsync(s, 17, 18);
        await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        var impact = await _buildings.GetConstraintsImpactAsync(s.Building.Id, await BuildingHoursAsync(s, "09:00", "17:00"));

        var affected = impact.Items.ShouldHaveSingleItem();
        impact.Count.ShouldBe(1);
        affected.Id.ShouldBe(late.Id);
        affected.HeldBy.ShouldBe("Jordan Reed");
        affected.PlaceName.ShouldBe("Room 1");
        affected.Kind.ShouldBe(ReservationKinds.Booking);
        affected.LocalStart.ShouldBe(Tomorrow.AddHours(17));
        affected.Reasons.ShouldBe(new[] { "Open 09:00–17:00 only" });

        (await _buildings.GetAsync(s.Building.Id)).Hours.Open.ShouldBe("07:00");
    }

    [Fact]
    public async Task Saving_keeps_affected_bookings_unless_the_admin_chooses_to_cancel_them()
    {
        var s = await CreateScenarioAsync();
        var late = await BookAsync(s, 17, 18);
        using (ActAs(Admin))
        {
            var kept = await _buildings.UpdateConstraintsAsync(s.Building.Id, await BuildingHoursAsync(s, "08:00", "17:30"));
            kept.CancelledBookings.ShouldBe(0);
        }

        (await StoredAsync(late.Id)).Status.ShouldBe(BookingStatus.Confirmed);

        using (ActAs(Admin))
        {
            var cancelled = await _buildings.UpdateConstraintsAsync(s.Building.Id, await BuildingHoursAsync(s, "09:00", "17:00", cancel: true));
            cancelled.CancelledBookings.ShouldBe(1);
        }

        var stored = await StoredAsync(late.Id);
        stored.Status.ShouldBe(BookingStatus.Cancelled);
        stored.CancelledByAdmin.ShouldBeTrue();
        stored.CancelledById.ShouldBe(Admin);
        stored.CancelReason.ShouldBe("Rules changed: Open 09:00–17:00 only");
    }

    [Fact]
    public async Task The_employee_still_sees_an_admin_cancelled_booking_with_why()
    {
        var s = await CreateScenarioAsync();
        var late = await BookAsync(s, 17, 18);
        using (ActAs(Admin))
        {
            await _buildings.UpdateConstraintsAsync(s.Building.Id, await BuildingHoursAsync(s, "09:00", "17:00", cancel: true));
        }

        using var _ = ActAs(s.UserId);
        var mine = await _bookings.GetMineAsync(new GetMyBookingsInput { From = Tomorrow, To = Tomorrow.AddDays(1) });

        var shown = mine.Items.ShouldHaveSingleItem();
        shown.Id.ShouldBe(late.Id);
        shown.Status.ShouldBe(nameof(BookingStatus.Cancelled));
        // Why it went is detail, not calendar data — it comes with the full booking.
        var full = await _bookings.GetAsync(late.Id);
        full.CancelledByAdmin.ShouldBeTrue();
        full.CancelReason.ShouldBe("Rules changed: Open 09:00–17:00 only");
    }

    [Fact]
    public async Task A_floor_shortening_the_longest_booking_lists_the_longer_ones()
    {
        var s = await CreateScenarioAsync();
        var long1 = await BookAsync(s, 9, 12);
        await BookAsync(s, 13, 14);
        var floor = await _floors.GetAsync(s.Floor.Id);

        using var _ = ActAs(Admin);
        var impact = await _floors.GetConstraintsImpactAsync(s.Floor.Id, new UpdateFloorConstraintsDto
        {
            MaxDurationMinutes = 60,
            ConcurrencyStamp = floor.ConcurrencyStamp,
        });

        impact.Items.ShouldHaveSingleItem().Id.ShouldBe(long1.Id);
        impact.Items[0].Reasons.ShouldBe(new[] { "Max 1h per booking" });
    }

    [Fact]
    public async Task A_room_raising_its_minimum_group_lists_the_smaller_bookings_and_can_cancel_them()
    {
        var s = await CreateScenarioAsync();
        var small = await BookAsync(s, 9, 10, attendees: 2);
        var big = await BookAsync(s, 11, 12, attendees: 6);
        var space = await _spaces.GetAsync(s.Space.Id);

        using var _ = ActAs(Admin);
        var result = await _spaces.UpdateConstraintsAsync(s.Space.Id, new UpdateSpaceConstraintsDto
        {
            MinAttendees = 4,
            ConcurrencyStamp = space.ConcurrencyStamp,
            CancelAffectedBookings = true,
        });

        result.CancelledBookings.ShouldBe(1);
        (await StoredAsync(small.Id)).Status.ShouldBe(BookingStatus.Cancelled);
        (await StoredAsync(big.Id)).Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task A_new_closure_lists_the_bookings_it_falls_on_and_cancels_them_with_its_reason()
    {
        var s = await CreateScenarioAsync();
        var hit = await BookAsync(s, 10, 11);
        var clear = await BookAsync(s, 15, 16);
        var closure = new CreateAvailabilityOverrideDto
        {
            Scope = OverrideScope.Floor,
            ScopeId = s.Floor.Id,
            StartsAt = new DateTimeOffset(Tomorrow.AddHours(9), TimeSpan.Zero),
            EndsAt = new DateTimeOffset(Tomorrow.AddHours(13), TimeSpan.Zero),
            Effect = OverrideEffect.Closed,
            ReasonCategory = ReasonCategory.Maintenance,
            ReasonDetail = "HVAC service",
        };

        using var _ = ActAs(Admin);
        (await _closures.GetCreateImpactAsync(closure)).Items.ShouldHaveSingleItem().Id.ShouldBe(hit.Id);

        closure.CancelAffectedBookings = true;
        await _closures.CreateAsync(closure);

        var stored = await StoredAsync(hit.Id);
        stored.Status.ShouldBe(BookingStatus.Cancelled);
        stored.CancelReason.ShouldBe("Closed: HVAC service");
        (await StoredAsync(clear.Id)).Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task A_special_opening_affects_nothing()
    {
        var s = await CreateScenarioAsync();
        await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        var impact = await _closures.GetCreateImpactAsync(new CreateAvailabilityOverrideDto
        {
            Scope = OverrideScope.Space,
            ScopeId = s.Space.Id,
            StartsAt = new DateTimeOffset(Tomorrow.AddHours(9), TimeSpan.Zero),
            EndsAt = new DateTimeOffset(Tomorrow.AddHours(13), TimeSpan.Zero),
            Effect = OverrideEffect.Open,
            ReasonCategory = ReasonCategory.Event,
        });

        impact.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Deleting_a_room_cancels_its_upcoming_bookings_and_leaves_the_rest()
    {
        var s = await CreateScenarioAsync();
        var upcoming = await BookAsync(s, 10, 11);
        // One that already started: written straight to the table, it must be left alone.
        var started = await WithUnitOfWorkAsync(() => _bookingRepository.InsertAsync(new Booking(
            Guid.NewGuid(), s.Space.Id, s.UserId, DateTimeOffset.UtcNow.AddMinutes(-30), DateTimeOffset.UtcNow.AddMinutes(30),
            attendees: 2, title: "Stand-up", resolvedConstraintsJson: "{}", idempotencyKey: Guid.NewGuid().ToString())));

        using var _ = ActAs(Admin);
        var impact = await _spaces.GetDeleteImpactAsync(s.Space.Id);
        impact.Items.ShouldHaveSingleItem().Reasons.ShouldBe(new[] { "The space was removed" });

        await _spaces.DeleteAsync(s.Space.Id);

        var stored = await StoredAsync(upcoming.Id);
        stored.Status.ShouldBe(BookingStatus.Cancelled);
        stored.CancelReason.ShouldBe("The space was removed");
        (await StoredAsync(started.Id)).Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Deleting_a_building_cancels_upcoming_bookings_on_every_floor()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        (await _buildings.GetDeleteImpactAsync(s.Building.Id)).Count.ShouldBe(1);
        await _buildings.DeleteAsync(s.Building.Id);

        (await StoredAsync(booking.Id)).CancelReason.ShouldBe("The building was removed");
    }

    [Fact]
    public async Task Deleting_a_floor_cancels_the_upcoming_bookings_on_it()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        await _floors.DeleteAsync(s.Floor.Id);

        var stored = await StoredAsync(booking.Id);
        stored.Status.ShouldBe(BookingStatus.Cancelled);
        stored.CancelledByAdmin.ShouldBeTrue();
        stored.CancelReason.ShouldBe("The floor was removed");
    }

    [Fact]
    public async Task Bookings_listen_for_a_removed_room_not_the_space_service()
    {
        // Anything that removes a room (a future import, another module) only has to announce
        // it; Bookings releases what it held there itself.
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        await WithUnitOfWorkAsync(() =>
            GetRequiredService<ILocalEventBus>().PublishAsync(new SpaceDeletedEvent(s.Space.Id, Admin)));

        var stored = await StoredAsync(booking.Id);
        stored.Status.ShouldBe(BookingStatus.Cancelled);
        stored.CancelledById.ShouldBe(Admin);
        stored.CancelReason.ShouldBe("The space was removed");
    }

    [Fact]
    public async Task After_the_building_is_deleted_the_employee_is_told_and_still_sees_their_calendar()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);
        using (ActAs(Admin))
        {
            var impact = await _buildings.GetDeleteImpactAsync(s.Building.Id);
            impact.AssignedEmployees.ShouldBe(1);
            await _buildings.DeleteAsync(s.Building.Id);
        }

        using var _ = ActAs(s.UserId);
        var mine = await _availability.GetMyBuildingAsync();
        mine.ShouldNotBeNull();
        mine.IsRemoved.ShouldBeTrue();
        mine.Name.ShouldBe(s.Building.FindName("en"));
        mine.Timezone.ShouldBe("UTC");
        mine.Floors.ShouldBeEmpty();

        var calendar = await _bookings.GetMineAsync(new GetMyBookingsInput { From = Tomorrow, To = Tomorrow.AddDays(1) });
        var shown = calendar.Items.ShouldHaveSingleItem();
        shown.Id.ShouldBe(booking.Id);
        shown.Status.ShouldBe(nameof(BookingStatus.Cancelled));
        (await _bookings.GetAsync(booking.Id)).CancelReason.ShouldBe("The building was removed");
    }

    [Fact]
    public async Task Restoring_the_building_gives_the_employee_it_back()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(Admin))
        {
            await _buildings.DeleteAsync(s.Building.Id);
            await _buildings.RestoreAsync(s.Building.Id);
        }

        using var _ = ActAs(s.UserId);
        var mine = await _availability.GetMyBuildingAsync();
        mine.ShouldNotBeNull().IsRemoved.ShouldBeFalse();
        mine.Floors.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Lowering_a_room_capacity_lists_the_bigger_bookings_and_can_cancel_them()
    {
        var s = await CreateScenarioAsync();
        var big = await BookAsync(s, 9, 10, attendees: 6);
        var small = await BookAsync(s, 11, 12, attendees: 3);
        var space = await _spaces.GetAsync(s.Space.Id);
        var input = new UpdateSpaceDto { Names = En(space.Name), SpaceTypeId = space.SpaceTypeId, Capacity = 4 };

        using var _ = ActAs(Admin);
        var impact = await _spaces.GetUpdateImpactAsync(s.Space.Id, input);
        impact.Items.ShouldHaveSingleItem().Id.ShouldBe(big.Id);
        impact.Items[0].Reasons.ShouldBe(new[] { "Seats 4 — you need 6" });

        input.CancelAffectedBookings = true;
        await _spaces.UpdateAsync(s.Space.Id, input);

        (await StoredAsync(big.Id)).CancelReason.ShouldBe("Rules changed: Seats 4 — you need 6");
        (await StoredAsync(small.Id)).Status.ShouldBe(BookingStatus.Confirmed);
    }
}
