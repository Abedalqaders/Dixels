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
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// The admin saves that can leave bookings behind — rules for a building, floor or room, a
/// room's capacity, a new closure — keep them, or cancel exactly the ones that no longer fit,
/// each with the rule it breaks (or the closure's reason). Whatever the save reads to get
/// there, the outcome and what it announces stay the same.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class AdminSaveTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBuildingsAppService _buildings;
    private readonly IFloorsAppService _floors;
    private readonly ISpacesAppService _spaces;
    private readonly IAvailabilityOverridesAppService _closures;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);
    private static readonly Guid Admin = Guid.NewGuid();

    public AdminSaveTests()
    {
        _buildings = GetRequiredService<IBuildingsAppService>();
        _floors = GetRequiredService<IFloorsAppService>();
        _spaces = GetRequiredService<ISpacesAppService>();
        _closures = GetRequiredService<IAvailabilityOverridesAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    /// <summary>A UTC building open 07:00–20:00 (up to 3h) with two floors of two rooms, and an employee in it.</summary>
    private sealed record Scenario(Guid UserId, Guid BuildingId, Guid[] FloorIds, Guid[][] Rooms)
    {
        public IEnumerable<Guid> AllRooms => Rooms.SelectMany(r => r);
    }

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "Saves HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0)),
            maxDurationMinutes: 180, maxHorizonDays: 30, minLeadMinutes: 0));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var floorIds = new Guid[2];
        var rooms = new Guid[2][];
        for (var f = 0; f < 2; f++)
        {
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", $"Level {f + 1}", f + 1));
            floorIds[f] = floor.Id;
            rooms[f] = new Guid[2];
            for (var r = 0; r < 2; r++)
            {
                rooms[f][r] = (await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(
                    new Space(Guid.NewGuid(), floor.Id, "en", $"Room {f + 1}.{r + 1}", spaceType.Id, 8))).Id;
            }
        }

        var user = new IdentityUser(Guid.NewGuid(), "emp" + Guid.NewGuid().ToString("N")[..8], $"{Guid.NewGuid():N}@test.io");
        user.SetBuildingId(building.Id);
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
        return new Scenario(user.Id, building.Id, floorIds, rooms);
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    /// <summary>A booking tomorrow, straight into the database (attendees 2).</summary>
    private Task<Guid> BookAsync(Scenario s, Guid spaceId, int startHour, int endHour, int attendees = 2) => WithUnitOfWorkAsync(async () =>
        (await _bookingRepository.InsertAsync(new Booking(
            Guid.NewGuid(), spaceId, s.UserId,
            new DateTimeOffset(Tomorrow.AddHours(startHour), TimeSpan.Zero),
            new DateTimeOffset(Tomorrow.AddHours(endHour), TimeSpan.Zero),
            attendees, title: "Planning", resolvedConstraintsJson: "{}", idempotencyKey: Guid.NewGuid().ToString()))).Id);

    private Task<Booking> StoredAsync(Guid id) => WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(id));

    private async Task<Dictionary<Guid, string?>> CancelReasonsAsync(IEnumerable<Guid> ids)
    {
        var result = new Dictionary<Guid, string?>();
        foreach (var id in ids)
        {
            var booking = await StoredAsync(id);
            result[id] = booking.Status == BookingStatus.Cancelled ? booking.CancelReason : null;
        }

        return result;
    }

    /// <summary>Records every event of a kind published while the returned handle is held.</summary>
    private (List<T> Seen, IDisposable Subscription) Listen<T>() where T : class
    {
        var seen = new List<T>();
        var subscription = GetRequiredService<ILocalEventBus>().Subscribe<T>(e =>
        {
            seen.Add(e);
            return Task.CompletedTask;
        });
        return (seen, subscription);
    }

    private async Task<UpdateBuildingConstraintsDto> BuildingRulesAsync(Scenario s, string open, string close, int maxMinutes, bool cancel)
    {
        var current = await _buildings.GetAsync(s.BuildingId);
        return new UpdateBuildingConstraintsDto
        {
            Days = current.Days,
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = open, Close = close },
            MaxDurationMinutes = maxMinutes,
            MaxHorizonDays = current.MaxHorizonDays,
            MinLeadMinutes = current.MinLeadMinutes,
            OwnOverlapPolicy = current.OwnOverlapPolicy,
            ConcurrencyStamp = current.ConcurrencyStamp,
            CancelAffectedBookings = cancel,
        };
    }

    private async Task<UpdateFloorConstraintsDto> FloorRulesAsync(Guid floorId, string close, bool cancel) => new()
    {
        Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "07:00", Close = close },
        ConcurrencyStamp = (await _floors.GetAsync(floorId)).ConcurrencyStamp,
        CancelAffectedBookings = cancel,
    };

    private static CreateAvailabilityOverrideDto Closure(OverrideScope scope, Guid scopeId, int startHour, int endHour, bool cancel) => new()
    {
        Scope = scope,
        ScopeId = scopeId,
        StartsAt = new DateTimeOffset(Tomorrow.AddHours(startHour), TimeSpan.Zero),
        EndsAt = new DateTimeOffset(Tomorrow.AddHours(endHour), TimeSpan.Zero),
        Effect = OverrideEffect.Closed,
        ReasonCategory = ReasonCategory.Maintenance,
        ReasonDetail = "Replacing the chairs",
        CancelAffectedBookings = cancel,
    };

    [Fact]
    public async Task A_building_rules_save_keeps_or_cancels_exactly_what_no_longer_fits_and_names_every_room()
    {
        var s = await CreateScenarioAsync();
        var fits = await BookAsync(s, s.Rooms[0][0], 10, 11);
        var late = await BookAsync(s, s.Rooms[0][1], 17, 18);
        var upstairsLate = await BookAsync(s, s.Rooms[1][0], 16, 17);

        var (events, subscription) = Listen<SpaceRulesChangedEvent>();
        using (subscription)
        using (ActAs(Admin))
        {
            (await _buildings.UpdateConstraintsAsync(s.BuildingId, await BuildingRulesAsync(s, "09:00", "16:00", 180, cancel: false))).CancelledBookings.ShouldBe(0);
            (await CancelReasonsAsync(new[] { fits, late, upstairsLate })).Values.ShouldAllBe(r => r == null);

            (await _buildings.UpdateConstraintsAsync(s.BuildingId, await BuildingRulesAsync(s, "09:00", "16:00", 180, cancel: true))).CancelledBookings.ShouldBe(2);
        }

        (await CancelReasonsAsync(new[] { fits, late, upstairsLate })).ShouldBe(new Dictionary<Guid, string?>
        {
            [fits] = null,
            [late] = "Rules changed: Open 09:00–16:00 only",
            [upstairsLate] = "Rules changed: Open 09:00–16:00 only",
        });

        // Keep or cancel, the event names every room in the building.
        events.Count.ShouldBe(2);
        events.ShouldAllBe(e => e.BuildingId == s.BuildingId);
        events.Select(e => e.SpaceIds.OrderBy(id => id)).ShouldAllBe(ids => ids.SequenceEqual(s.AllRooms.OrderBy(id => id)));
        events[0].Affected.ShouldBeNull();
        events[1].Affected!.Select(a => a.Id).OrderBy(id => id).ShouldBe(new[] { late, upstairsLate }.OrderBy(id => id));
    }

    [Fact]
    public async Task A_big_cancel_goes_in_rounds_and_still_cancels_every_one_without_emails()
    {
        var s = await CreateScenarioAsync();
        // 1,200 one-minute bookings from 16:00 (the new closing time) on, 300 a room; 10 that still fit.
        var outside = new List<Guid>();
        var inside = new List<Guid>();
        await WithUnitOfWorkAsync(async () =>
        {
            foreach (var room in s.AllRooms)
            {
                for (var i = 0; i < 300; i++)
                {
                    var start = new DateTimeOffset(Tomorrow.AddHours(16).AddMinutes(i), TimeSpan.Zero);
                    outside.Add((await _bookingRepository.InsertAsync(new Booking(Guid.NewGuid(), room, s.UserId, start, start.AddMinutes(1),
                        2, title: "Slot", resolvedConstraintsJson: "{}", idempotencyKey: Guid.NewGuid().ToString()))).Id);
                }
            }

            for (var i = 0; i < 10; i++)
            {
                var start = new DateTimeOffset(Tomorrow.AddHours(10).AddMinutes(i), TimeSpan.Zero);
                inside.Add((await _bookingRepository.InsertAsync(new Booking(Guid.NewGuid(), s.Rooms[0][0], s.UserId, start, start.AddMinutes(1),
                    2, title: "Slot", resolvedConstraintsJson: "{}", idempotencyKey: Guid.NewGuid().ToString()))).Id);
            }
        });
        var emails = GetRequiredService<Dixels.Emailing.FakeEmailSender>();
        emails.Clear();

        var (rounds, subscription) = Listen<BookingsCancelledEvent>();
        using (subscription)
        using (ActAs(Admin))
        {
            (await _buildings.UpdateConstraintsAsync(s.BuildingId, await BuildingRulesAsync(s, "09:00", "16:00", 180, cancel: true)))
                .CancelledBookings.ShouldBe(1200);
        }

        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetListAsync(b => b.UserId == s.UserId));
        stored.Where(b => outside.Contains(b.Id)).ShouldAllBe(b =>
            b.Status == BookingStatus.Cancelled && b.CancelledByAdmin && b.CancelReason == "Rules changed: Open 09:00–16:00 only");
        stored.Where(b => inside.Contains(b.Id)).ShouldAllBe(b => b.Status == BookingStatus.Confirmed);

        // Three rounds of at most 500, each an admin cancel: nobody is emailed for those.
        rounds.Select(r => r.Bookings.Count).ShouldBe(new[] { 500, 500, 200 });
        rounds.ShouldAllBe(r => r.ByAdmin);
        emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_floor_rules_save_keeps_or_cancels_only_on_that_floor()
    {
        var s = await CreateScenarioAsync();
        var late = await BookAsync(s, s.Rooms[0][0], 17, 18);
        var upstairsLate = await BookAsync(s, s.Rooms[1][0], 17, 18);

        var (events, subscription) = Listen<SpaceRulesChangedEvent>();
        using (subscription)
        using (ActAs(Admin))
        {
            (await _floors.UpdateConstraintsAsync(s.FloorIds[0], await FloorRulesAsync(s.FloorIds[0], "16:00", cancel: false))).CancelledBookings.ShouldBe(0);
            (await _floors.UpdateConstraintsAsync(s.FloorIds[0], await FloorRulesAsync(s.FloorIds[0], "16:00", cancel: true))).CancelledBookings.ShouldBe(1);
        }

        (await CancelReasonsAsync(new[] { late, upstairsLate })).ShouldBe(new Dictionary<Guid, string?>
        {
            [late] = "Rules changed: Open 07:00–16:00 only",
            [upstairsLate] = null,
        });
        events.Select(e => e.SpaceIds.OrderBy(id => id)).ShouldAllBe(ids => ids.SequenceEqual(s.Rooms[0].OrderBy(id => id)));
    }

    [Fact]
    public async Task A_save_cancels_the_same_bookings_with_the_same_reasons_the_preview_shows()
    {
        var s = await CreateScenarioAsync();
        // Some break the hours, some the length, one both (its first rule is what it shows).
        var bookings = new List<Guid>
        {
            await BookAsync(s, s.Rooms[0][0], 17, 18),
            await BookAsync(s, s.Rooms[0][1], 9, 12),
            await BookAsync(s, s.Rooms[1][0], 15, 18),
            await BookAsync(s, s.Rooms[1][1], 10, 11),
        };

        using var _ = ActAs(Admin);
        var input = await BuildingRulesAsync(s, "09:00", "16:00", 120, cancel: true);
        var preview = await _buildings.GetConstraintsImpactAsync(s.BuildingId, input);
        var shown = preview.Items.ToDictionary(i => i.Id, i => (string?)$"Rules changed: {i.Reasons[0]}");
        shown.Count.ShouldBe(3);

        await _buildings.UpdateConstraintsAsync(s.BuildingId, input);

        var reasons = await CancelReasonsAsync(bookings);
        reasons.Where(r => r.Value != null).ToDictionary(r => r.Key, r => r.Value).ShouldBe(shown, ignoreOrder: true);
    }

    [Fact]
    public async Task A_closure_keeps_or_cancels_with_its_reason_and_passes_on_what_it_found()
    {
        var s = await CreateScenarioAsync();
        var during = await BookAsync(s, s.Rooms[0][0], 10, 11);
        var after = await BookAsync(s, s.Rooms[0][1], 14, 15);
        var otherFloor = await BookAsync(s, s.Rooms[1][0], 10, 11);

        var (events, subscription) = Listen<ClosureCreatedEvent>();
        using (subscription)
        using (ActAs(Admin))
        {
            await _closures.CreateAsync(Closure(OverrideScope.Floor, s.FloorIds[0], 9, 12, cancel: false));
            (await CancelReasonsAsync(new[] { during, after, otherFloor })).Values.ShouldAllBe(r => r == null);

            await _closures.CreateAsync(Closure(OverrideScope.Floor, s.FloorIds[0], 9, 12, cancel: true));
        }

        (await CancelReasonsAsync(new[] { during, after, otherFloor })).ShouldBe(new Dictionary<Guid, string?>
        {
            [during] = "Closed: Replacing the chairs",
            [after] = null,
            [otherFloor] = null,
        });

        events.Count.ShouldBe(2);
        events.Select(e => e.SpaceIds.OrderBy(id => id)).ShouldAllBe(ids => ids.SequenceEqual(s.Rooms[0].OrderBy(id => id)));
        events[0].Affected.ShouldBeNull();
        // Found before saving and carried on, so the listener cancels it without checking again.
        events[1].Affected!.Select(a => a.Id).ShouldBe(new[] { during });
    }

    [Fact]
    public async Task A_closure_on_one_room_or_the_whole_building_reaches_those_rooms()
    {
        var s = await CreateScenarioAsync();
        var (events, subscription) = Listen<ClosureCreatedEvent>();
        using (subscription)
        using (ActAs(Admin))
        {
            await _closures.CreateAsync(Closure(OverrideScope.Space, s.Rooms[1][1], 9, 12, cancel: false));
            await _closures.CreateAsync(Closure(OverrideScope.Building, s.BuildingId, 9, 12, cancel: false));
        }

        events[0].SpaceIds.ShouldBe(new[] { s.Rooms[1][1] });
        events[1].SpaceIds.OrderBy(id => id).ShouldBe(s.AllRooms.OrderBy(id => id));
        events.ShouldAllBe(e => e.BuildingId == s.BuildingId);
    }

    [Theory]
    [InlineData(OverrideScope.Space, OverrideEffect.Closed)]
    [InlineData(OverrideScope.Floor, OverrideEffect.Closed)]
    [InlineData(OverrideScope.Building, OverrideEffect.Closed)]
    [InlineData(OverrideScope.Space, OverrideEffect.Open)]
    public async Task A_closure_or_opening_on_a_removed_room_floor_or_building_is_not_found_and_saves_nothing(OverrideScope scope, OverrideEffect effect)
    {
        var s = await CreateScenarioAsync();
        var scopeId = scope switch
        {
            OverrideScope.Space => s.Rooms[0][0],
            OverrideScope.Floor => s.FloorIds[0],
            _ => s.BuildingId,
        };
        await WithUnitOfWorkAsync(async () =>
        {
            switch (scope)
            {
                case OverrideScope.Space: await GetRequiredService<IRepository<Space, Guid>>().DeleteAsync(scopeId); break;
                case OverrideScope.Floor: await GetRequiredService<IRepository<Floor, Guid>>().DeleteAsync(scopeId); break;
                default: await GetRequiredService<IRepository<Building, Guid>>().DeleteAsync(scopeId); break;
            }
        });

        var input = Closure(scope, scopeId, 9, 12, cancel: true);
        input.Effect = effect;
        using (ActAs(Admin))
        {
            var notFound = await Should.ThrowAsync<EntityNotFoundException>(() => _closures.CreateAsync(input));
            notFound.EntityType.ShouldBe(scope switch
            {
                OverrideScope.Space => typeof(Space),
                OverrideScope.Floor => typeof(Floor),
                _ => typeof(Building),
            });
            notFound.Id.ShouldBe(scopeId);
        }

        (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AvailabilityOverride, Guid>>().CountAsync(o => o.ScopeId == scopeId))).ShouldBe(0);
    }

    [Fact]
    public async Task A_special_opening_announces_nothing()
    {
        var s = await CreateScenarioAsync();
        var (events, subscription) = Listen<ClosureCreatedEvent>();
        using (subscription)
        using (ActAs(Admin))
        {
            var opening = Closure(OverrideScope.Building, s.BuildingId, 21, 22, cancel: true);
            opening.Effect = OverrideEffect.Open;
            await _closures.CreateAsync(opening);
        }

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_time_zone_change_is_refused_while_bookings_are_ahead_and_allowed_without()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(Admin);
        var details = await _buildings.GetAsync(s.BuildingId);
        var input = new UpdateBuildingDto { Names = details.Names, BuildingNumber = details.BuildingNumber, Timezone = "Europe/London" };

        await BookAsync(s, s.Rooms[1][1], 10, 11);
        var refused = await Should.ThrowAsync<BusinessException>(() => _buildings.UpdateAsync(s.BuildingId, input));
        refused.Code.ShouldBe(DixelsDomainErrorCodes.TimezoneChangeWithBookings);
        refused.Data["count"].ShouldBe(1);

        var empty = await CreateScenarioAsync();
        var emptyDetails = await _buildings.GetAsync(empty.BuildingId);
        (await _buildings.UpdateAsync(empty.BuildingId, new UpdateBuildingDto { Names = emptyDetails.Names, Timezone = "Europe/London" }))
            .Timezone.ShouldBe("Europe/London");
    }
}
