using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Identity;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingsAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookingsAppService;
    private readonly IAvailabilityAppService _availabilityAppService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    // Tomorrow, so lead time and "in the past" never interfere; the building below is
    // UTC and open 24/7 with a 30-day horizon, so only the rule under test can fail.
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    public BookingsAppServiceTests()
    {
        _bookingsAppService = GetRequiredService<IBookingsAppService>();
        _availabilityAppService = GetRequiredService<IAvailabilityAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
        _spaceTypeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Scenario(Guid UserId, Building Building, Floor Floor, Space Space);

    /// <summary>A UTC, 24/7 building (max 2h, 30-day horizon, no lead time) with one 8-seat
    /// room needing at least 2 people, and an employee assigned to it.</summary>
    // One unit of work around the whole setup: IdentityUserManager needs an ambient one.
    private Task<Scenario> CreateScenarioAsync(bool assign = true) => WithUnitOfWorkAsync(async () =>
    {
        var building = await _buildingRepository.InsertAsync(new Building(
            Guid.NewGuid(), "en", "Test HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));

        var floor = await _floorRepository.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));

        var spaceType = await _spaceTypeRepository.FirstAsync();
        var space = new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, capacity: 8);
        space.SetMinAttendees(2);
        await _spaceRepository.InsertAsync(space);

        var user = new IdentityUser(Guid.NewGuid(), "emp" + Guid.NewGuid().ToString("N")[..8], $"{Guid.NewGuid():N}@test.io");
        if (assign)
        {
            user.SetBuildingId(building.Id);
        }

        (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, building, floor, space);
    });

    private Task<long> CountBookingsAsync() => WithUnitOfWorkAsync(() => _bookingRepository.GetCountAsync());

    private IDisposable ActAs(Guid userId)
    {
        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
        })));
    }

    private static CreateBookingDto Request(Guid spaceId, int startHour, int endHour, int attendees = 2, string? key = null)
        => new()
        {
            SpaceId = spaceId,
            LocalStart = Tomorrow.AddHours(startHour),
            LocalEnd = Tomorrow.AddHours(endHour),
            Attendees = attendees,
            Title = "Planning",
            IdempotencyKey = key ?? Guid.NewGuid().ToString(),
        };

    [Fact]
    public async Task Preview_of_a_valid_request_is_valid()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var preview = await _bookingsAppService.PreviewAsync(Request(s.Space.Id, 10, 11));

        preview.IsValid.ShouldBeTrue();
        preview.Violations.ShouldBeEmpty();
        preview.Timezone.ShouldBe("UTC");
        preview.StartsAt.ShouldBe(new DateTimeOffset(Tomorrow.AddHours(10), TimeSpan.Zero));
    }

    [Fact]
    public async Task Preview_returns_every_violation_with_localized_messages_naming_the_level()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var preview = await _bookingsAppService.PreviewAsync(Request(s.Space.Id, 10, 13, attendees: 1));

        preview.IsValid.ShouldBeFalse();
        preview.Violations.Select(v => v.Code).ShouldBe(new[]
        {
            DixelsDomainErrorCodes.BookingBelowMinAttendees,
            DixelsDomainErrorCodes.BookingTooLong,
        });

        preview.Violations[0].Level.ShouldBe("Space");
        preview.Violations[0].Message.ShouldContain("at least 2 attendees (Space rule)");
        preview.Violations[1].Message.ShouldContain("at most 2h (Building rule)");
        preview.Violations[1].Message.ShouldContain("you asked for 3h");
    }

    [Fact]
    public async Task Create_stores_utc_times_and_the_rules_it_was_accepted_under()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var created = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11, attendees: 3));

        created.Status.ShouldBe(nameof(BookingStatus.Confirmed));
        created.SpaceName.ShouldBe("Room 1");
        created.FloorName.ShouldBe("Level 1");
        created.LocalStart.ShouldBe(Tomorrow.AddHours(10));

        var stored = await _bookingRepository.GetAsync(created.Id);
        stored.UserId.ShouldBe(s.UserId);
        stored.StartsAt.ShouldBe(new DateTimeOffset(Tomorrow.AddHours(10), TimeSpan.Zero));
        stored.EndsAt.ShouldBe(new DateTimeOffset(Tomorrow.AddHours(11), TimeSpan.Zero));
        stored.Attendees.ShouldBe(3);
        stored.ResolvedConstraintsJson.ShouldContain("\"maxDurationMinutes\":120,\"maxDurationSource\":\"Building\"");
    }

    [Fact]
    public async Task A_blank_title_is_stored_empty()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var request = Request(s.Space.Id, 10, 11);
        request.Title = "   ";

        (await _bookingsAppService.CreateAsync(request)).Title.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task An_overlapping_booking_is_rejected()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 12));

        var ex = await Should.ThrowAsync<BookingRejectedException>(
            () => _bookingsAppService.CreateAsync(Request(s.Space.Id, 11, 12)));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingOverlap);
        (await CountBookingsAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Back_to_back_bookings_are_both_accepted()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));
        await _bookingsAppService.CreateAsync(Request(s.Space.Id, 11, 12));

        (await CountBookingsAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task A_cancelled_booking_does_not_block_the_slot()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var first = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));

        var stored = await _bookingRepository.GetAsync(first.Id);
        stored.Cancel(s.UserId, DateTimeOffset.UtcNow, "Plans changed", byAdmin: false);
        await _bookingRepository.UpdateAsync(stored);

        var second = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));
        second.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public async Task Retrying_with_the_same_idempotency_key_returns_the_original_booking()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var first = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11, key: "attempt-1"));
        var retry = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11, key: "attempt-1"));

        retry.Id.ShouldBe(first.Id);
        (await CountBookingsAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_for_a_different_request_is_rejected()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11, key: "attempt-1"));

        var ex = await Should.ThrowAsync<BusinessException>(
            () => _bookingsAppService.CreateAsync(Request(s.Space.Id, 14, 15, key: "attempt-1")));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingIdempotencyKeyReused);
    }

    [Fact]
    public async Task An_employee_cannot_book_outside_their_assigned_building()
    {
        var s = await CreateScenarioAsync(assign: false);
        using var _ = ActAs(s.UserId);

        var ex = await Should.ThrowAsync<BusinessException>(
            () => _bookingsAppService.PreviewAsync(Request(s.Space.Id, 10, 11)));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingNotAssignedToBuilding);
    }

    [Fact]
    public async Task End_before_start_is_rejected_before_any_rule_runs()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var ex = await Should.ThrowAsync<BusinessException>(
            () => _bookingsAppService.PreviewAsync(Request(s.Space.Id, 11, 10)));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingInvalidTimeRange);
    }

    [Fact]
    public async Task A_deleted_space_cannot_be_booked()
    {
        var s = await CreateScenarioAsync();
        await _spaceRepository.DeleteAsync(s.Space.Id);
        using var _ = ActAs(s.UserId);

        await Should.ThrowAsync<EntityNotFoundException>(
            () => _bookingsAppService.PreviewAsync(Request(s.Space.Id, 10, 11)));
    }

    [Fact]
    public async Task My_building_lists_its_spaces_with_resolved_limits_and_their_source()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var building = await _availabilityAppService.GetMyBuildingAsync();

        building.ShouldNotBeNull();
        building.Id.ShouldBe(s.Building.Id);
        building.Timezone.ShouldBe("UTC");
        building.SlotMinutes.ShouldBe(15);

        var space = building.Floors.ShouldHaveSingleItem().Spaces.ShouldHaveSingleItem();
        space.Name.ShouldBe("Room 1");
        space.Capacity.ShouldBe(8);
        space.MinAttendees.ShouldBe(2);
        space.MaxDurationMinutes.Value.ShouldBe(120);
        space.MaxDurationMinutes.Source.ShouldBe("Building");
        space.Hours.Value.IsOpen24Hours.ShouldBeTrue();
    }

    [Fact]
    public async Task My_building_is_null_for_an_unassigned_employee()
    {
        var s = await CreateScenarioAsync(assign: false);
        using var _ = ActAs(s.UserId);

        (await _availabilityAppService.GetMyBuildingAsync()).ShouldBeNull();
    }

    private static SearchAvailabilityInput Search(int startHour, int endHour, int attendees = 2)
        => new()
        {
            LocalStart = Tomorrow.AddHours(startHour),
            LocalEnd = Tomorrow.AddHours(endHour),
            Attendees = attendees,
        };

    [Fact]
    public async Task Search_lists_a_free_space_with_how_long_it_stays_free()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(s.Space.Id, 14, 15));

        var result = await _availabilityAppService.SearchAsync(Search(10, 11));

        var room = result.Spaces.ShouldHaveSingleItem();
        room.IsAvailable.ShouldBeTrue();
        room.Violations.ShouldBeEmpty();
        room.FreeUntil.ShouldBe("14:00");
        room.Busy.ShouldHaveSingleItem().StartMinute.ShouldBe(14 * 60);
        room.Busy[0].IsMine.ShouldBeTrue();
        room.Open.ShouldHaveSingleItem().EndMinute.ShouldBe(24 * 60);
    }

    [Fact]
    public async Task Search_marks_a_booked_space_unavailable_and_suggests_the_next_free_start()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 12));

        var room = (await _availabilityAppService.SearchAsync(Search(10, 11))).Spaces.ShouldHaveSingleItem();

        room.IsAvailable.ShouldBeFalse();
        room.Violations.ShouldHaveSingleItem().Code.ShouldBe(DixelsDomainErrorCodes.BookingOverlap);
        room.Violations[0].ShortMessage.ShouldBe("Already booked at that time");
        room.NextFreeStart.ShouldBe("12:00");
    }

    [Fact]
    public async Task Search_names_the_space_type_in_the_readers_language()
    {
        var s = await CreateScenarioAsync();
        var spaceType = await GetRequiredService<ISpaceTypesAppService>().CreateAsync(new CreateSpaceTypeDto
        {
            Names =
            [
                new() { Language = "en", Name = "Phone booth" },
                new() { Language = "ar", Name = "كابينة هاتف" },
            ],
        });
        await WithUnitOfWorkAsync(async () =>
        {
            var space = await _spaceRepository.GetAsync(s.Space.Id);
            space.SetSpaceType(spaceType.Id);
        });
        using var _ = ActAs(s.UserId);

        using (CultureHelper.Use("ar"))
        {
            (await _availabilityAppService.SearchAsync(Search(10, 11))).Spaces.ShouldHaveSingleItem().Space.SpaceTypeName.ShouldBe("كابينة هاتف");
        }

        using (CultureHelper.Use("en"))
        {
            (await _availabilityAppService.SearchAsync(Search(10, 11))).Spaces.ShouldHaveSingleItem().Space.SpaceTypeName.ShouldBe("Phone booth");
        }
    }

    [Fact]
    public async Task Search_gives_no_next_time_when_the_space_is_simply_too_small()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var room = (await _availabilityAppService.SearchAsync(Search(10, 11, attendees: 9))).Spaces.ShouldHaveSingleItem();

        room.IsAvailable.ShouldBeFalse();
        room.Violations.ShouldHaveSingleItem().ShortMessage.ShouldBe("Seats 8 — you need 9");
        room.NextFreeStart.ShouldBeNull();
    }

    [Fact]
    public async Task Search_rejects_a_window_that_runs_into_the_next_day()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var ex = await Should.ThrowAsync<BusinessException>(() => _availabilityAppService.SearchAsync(new SearchAvailabilityInput
        {
            LocalStart = Tomorrow.AddHours(23),
            LocalEnd = Tomorrow.AddHours(25),
            Attendees = 2,
        }));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingInvalidTimeRange);
    }

    // ---- One room's days, for the booking form ----

    [Fact]
    public async Task Space_days_give_the_open_hours_closed_days_closures_and_bookings()
    {
        var s = await CreateScenarioAsync();
        var closedDay = Tomorrow.AddDays(1).DayOfWeek;
        await WithUnitOfWorkAsync(async () =>
        {
            // The room: 08:00–18:00, closed on the day after tomorrow's weekday.
            var space = await _spaceRepository.GetAsync(s.Space.Id);
            space.SetOwnOperatingHours(new OperatingWindow(false, new TimeOnly(8, 0), new TimeOnly(18, 0)), s.Building.Hours);
            space.SetOwnOperatingDays(OperatingDays.FromDayOfWeeks(Enum.GetValues<DayOfWeek>().Where(d => d != closedDay)), s.Building.Days);

            // The whole building closed 12:00–14:00 two days after tomorrow.
            await GetRequiredService<IRepository<AvailabilityOverride, Guid>>().InsertAsync(new AvailabilityOverride(
                Guid.NewGuid(), OverrideScope.Building, s.Building.Id,
                new DateTimeOffset(Tomorrow.AddDays(2).AddHours(12), TimeSpan.Zero),
                new DateTimeOffset(Tomorrow.AddDays(2).AddHours(14), TimeSpan.Zero),
                OverrideEffect.Closed, ReasonCategory.Maintenance));
        });
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));

        var days = (await _availabilityAppService.GetSpaceDaysAsync(s.Space.Id, new GetSpaceDaysInput { From = Day(0), To = Day(2) })).Days;

        days.Select(d => d.Date).ShouldBe(new[] { Day(0), Day(1), Day(2) });

        var open = days[0].Open.ShouldHaveSingleItem();
        (open.StartMinute, open.EndMinute).ShouldBe((8 * 60, 18 * 60));
        var busy = days[0].Busy.ShouldHaveSingleItem();
        (busy.StartMinute, busy.EndMinute, busy.IsMine).ShouldBe((10 * 60, 11 * 60, true));

        days[1].Open.ShouldBeEmpty();

        var closed = days[2].Closed.ShouldHaveSingleItem();
        (closed.StartMinute, closed.EndMinute).ShouldBe((12 * 60, 14 * 60));
        days[2].Busy.ShouldBeEmpty();
    }

    [Fact]
    public async Task Space_days_run_from_today_to_the_last_bookable_date_at_most()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var days = (await _availabilityAppService.GetSpaceDaysAsync(s.Space.Id, new GetSpaceDaysInput
        {
            From = today.AddDays(-5),
            To = today.AddDays(100),
        })).Days;

        days.First().Date.ShouldBe(today);
        days.Last().Date.ShouldBe(today.AddDays(30));
    }

    [Fact]
    public async Task Space_days_are_refused_outside_the_employees_building()
    {
        var s = await CreateScenarioAsync(assign: false);
        using var _ = ActAs(s.UserId);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _availabilityAppService.GetSpaceDaysAsync(s.Space.Id, new GetSpaceDaysInput { From = Day(0), To = Day(1) }));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingNotAssignedToBuilding);
    }

    [Fact]
    public void The_employee_role_is_seeded_with_the_real_booking_permission_names()
    {
        RoleDataSeedContributor.EmployeePermissions.ShouldBe(new[]
        {
            DixelsPermissions.Bookings.Default,
            DixelsPermissions.Bookings.Create,
            DixelsPermissions.Bookings.Cancel,
        });
    }

    // ---- One person, two bookings at once (the building's OwnOverlapPolicy) ----

    private Task<Space> AddSpaceAsync(Scenario s, string name) => WithUnitOfWorkAsync(async () =>
    {
        var spaceType = await _spaceTypeRepository.FirstAsync();
        return await _spaceRepository.InsertAsync(new Space(Guid.NewGuid(), s.Floor.Id, "en", name, spaceType.Id, capacity: 8));
    });

    private Task SetPolicyAsync(Scenario s, OwnOverlapPolicy policy) => WithUnitOfWorkAsync(async () =>
    {
        var building = await _buildingRepository.GetAsync(s.Building.Id);
        building.SetOwnOverlapPolicy(policy);
        await _buildingRepository.UpdateAsync(building);
    });

    [Fact]
    public async Task A_new_building_warns_about_a_second_booking_at_the_same_time()
    {
        var s = await CreateScenarioAsync();
        var desk = await AddSpaceAsync(s, "Desk 7");
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(desk.Id, 10, 12));

        var preview = await _bookingsAppService.PreviewAsync(Request(s.Space.Id, 11, 12));

        preview.IsValid.ShouldBeTrue();
        var warning = preview.Warnings.ShouldHaveSingleItem();
        warning.Code.ShouldBe(DixelsDomainErrorCodes.BookingOwnOverlapWarning);
        warning.Message.ShouldStartWith("Heads-up: you already have Desk 7 booked ");
        warning.Message.ShouldEndWith("10:00–12:00.");

        (await _bookingsAppService.CreateAsync(Request(s.Space.Id, 11, 12))).Status.ShouldBe(nameof(BookingStatus.Confirmed));
    }

    [Fact]
    public async Task Allow_says_nothing_about_a_second_booking()
    {
        var s = await CreateScenarioAsync();
        await SetPolicyAsync(s, OwnOverlapPolicy.Allow);
        var desk = await AddSpaceAsync(s, "Desk 7");
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(desk.Id, 10, 12));

        var preview = await _bookingsAppService.PreviewAsync(Request(s.Space.Id, 11, 12));

        preview.IsValid.ShouldBeTrue();
        preview.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Block_refuses_a_second_booking_at_the_same_time_but_allows_back_to_back()
    {
        var s = await CreateScenarioAsync();
        await SetPolicyAsync(s, OwnOverlapPolicy.Block);
        var desk = await AddSpaceAsync(s, "Desk 7");
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(desk.Id, 10, 12));

        var preview = await _bookingsAppService.PreviewAsync(Request(s.Space.Id, 11, 12));
        preview.IsValid.ShouldBeFalse();
        preview.Violations.ShouldHaveSingleItem().Code.ShouldBe(DixelsDomainErrorCodes.BookingOwnOverlap);

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.CreateAsync(Request(s.Space.Id, 11, 12)));
        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingOwnOverlap);

        (await _bookingsAppService.CreateAsync(Request(s.Space.Id, 12, 13))).Status.ShouldBe(nameof(BookingStatus.Confirmed));
    }

    [Fact]
    public async Task A_cancelled_booking_is_no_clash()
    {
        var s = await CreateScenarioAsync();
        await SetPolicyAsync(s, OwnOverlapPolicy.Block);
        var desk = await AddSpaceAsync(s, "Desk 7");
        using var _ = ActAs(s.UserId);
        var first = await _bookingsAppService.CreateAsync(Request(desk.Id, 10, 12));
        await _bookingsAppService.CancelAsync(first.Id, new CancelBookingDto());

        (await _bookingsAppService.PreviewAsync(Request(s.Space.Id, 10, 12))).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Search_warns_once_for_the_whole_search_under_warn()
    {
        var s = await CreateScenarioAsync();
        var desk = await AddSpaceAsync(s, "Desk 7");
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(desk.Id, 10, 11));

        var result = await _availabilityAppService.SearchAsync(Search(10, 11));

        result.Warnings.ShouldHaveSingleItem().Code.ShouldBe(DixelsDomainErrorCodes.BookingOwnOverlapWarning);
        result.Spaces.Single(r => r.Space.Id == s.Space.Id).IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Search_rules_out_every_other_room_under_block()
    {
        var s = await CreateScenarioAsync();
        await SetPolicyAsync(s, OwnOverlapPolicy.Block);
        var desk = await AddSpaceAsync(s, "Desk 7");
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(desk.Id, 10, 11));

        var result = await _availabilityAppService.SearchAsync(Search(10, 11));

        result.Warnings.ShouldBeEmpty();
        var room = result.Spaces.Single(r => r.Space.Id == s.Space.Id);
        room.IsAvailable.ShouldBeFalse();
        room.Violations.ShouldHaveSingleItem().ShortMessage.ShouldBe("You're in Desk 7 then");
        // The desk itself just says it's booked — not "you're in Desk 7" on top.
        result.Spaces.Single(r => r.Space.Id == desk.Id).Violations.ShouldHaveSingleItem().Code.ShouldBe(DixelsDomainErrorCodes.BookingOverlap);
    }

    // ---- Recurring bookings ----

    private static DateOnly Day(int offset) => DateOnly.FromDateTime(Tomorrow.AddDays(offset));

    /// <summary>Daily 10:00–11:00 from tomorrow for <paramref name="days"/> days.</summary>
    private static CreateSeriesDto Daily(Guid spaceId, int days, int attendees = 2, params int[] skip) => new()
    {
        SpaceId = spaceId,
        LocalStart = Tomorrow.AddHours(10),
        LocalEnd = Tomorrow.AddHours(11),
        Attendees = attendees,
        Title = "Stand-up",
        Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = Day(days - 1) },
        SkipDates = skip.Select(Day).ToList(),
        IdempotencyKey = Guid.NewGuid().ToString(),
    };

    [Fact]
    public async Task Series_preview_checks_every_date_and_marks_the_ones_that_fail()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        // Day 2 at 10:00 is already booked.
        await _bookingsAppService.CreateAsync(new CreateBookingDto
        {
            SpaceId = s.Space.Id, LocalStart = Tomorrow.AddDays(2).AddHours(10), LocalEnd = Tomorrow.AddDays(2).AddHours(11),
            Attendees = 2, IdempotencyKey = Guid.NewGuid().ToString(),
        });

        var preview = await _bookingsAppService.PreviewSeriesAsync(Daily(s.Space.Id, 5));

        preview.SeriesViolations.ShouldBeEmpty();
        preview.Occurrences.Select(o => o.Date).ShouldBe(Enumerable.Range(0, 5).Select(Day));
        preview.Occurrences.Select(o => o.IsValid).ShouldBe(new[] { true, true, false, true, true });
        preview.Occurrences[2].Violations.ShouldHaveSingleItem().Code.ShouldBe(DixelsDomainErrorCodes.BookingOverlap);
        preview.Occurrences[3].LocalStart.ShouldBe(Tomorrow.AddDays(3).AddHours(10));
        preview.BookableCount.ShouldBe(4);
    }

    [Fact]
    public async Task A_rule_every_date_breaks_alike_is_reported_once_for_the_series()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var preview = await _bookingsAppService.PreviewSeriesAsync(Daily(s.Space.Id, 5, attendees: 9));

        preview.SeriesViolations.ShouldHaveSingleItem().Code.ShouldBe(DixelsDomainErrorCodes.BookingOverCapacity);
        preview.Occurrences.ShouldAllBe(o => !o.IsValid && o.Violations.Count == 0);
        preview.BookableCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_series_can_run_past_the_normal_horizon_up_to_the_series_one()
    {
        var s = await CreateScenarioAsync(); // 30-day horizon, 90-day series horizon
        using var _ = ActAs(s.UserId);
        var weekly = Daily(s.Space.Id, 1);
        weekly.Recurrence = new RecurrenceDto
        {
            Frequency = RecurrenceFrequency.Weekly, Interval = 1,
            Weekdays = new[] { (int)Tomorrow.DayOfWeek }, EndDate = Day(80),
        };

        var preview = await _bookingsAppService.PreviewSeriesAsync(weekly);
        preview.Occurrences.Last().Date.ShouldBeGreaterThan(Day(30));
        preview.Occurrences.ShouldAllBe(o => o.IsValid);

        weekly.Recurrence.EndDate = Day(95);
        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.PreviewSeriesAsync(weekly));
        ex.Code.ShouldBe(DixelsDomainErrorCodes.SeriesBeyondHorizon);
    }

    [Fact]
    public async Task Creating_books_every_ticked_date_as_one_series()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var created = await _bookingsAppService.CreateSeriesAsync(Daily(s.Space.Id, 5, skip: 2));

        created.Bookings.Count.ShouldBe(4);
        created.Bookings.ShouldAllBe(b => b.SeriesId == created.SeriesId && b.Title == "Stand-up");
        created.Bookings.Select(b => DateOnly.FromDateTime(b.LocalStart)).ShouldBe(new[] { Day(0), Day(1), Day(3), Day(4) });

        var mine = await _bookingsAppService.GetMineAsync(Days(0, 5));
        mine.Items.Count.ShouldBe(4);
        mine.Items.ShouldAllBe(b => b.SeriesId == created.SeriesId);

        // The rule itself isn't in the calendar's light list — it comes with the full booking.
        var full = await _bookingsAppService.GetAsync(mine.Items[0].Id);
        full.Recurrence.ShouldNotBeNull().Frequency.ShouldBe(RecurrenceFrequency.Daily);
        full.Recurrence!.EndDate.ShouldBe(Day(4));
    }

    [Fact]
    public async Task If_a_ticked_date_was_taken_since_the_preview_nothing_is_booked()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(new CreateBookingDto
        {
            SpaceId = s.Space.Id, LocalStart = Tomorrow.AddDays(3).AddHours(10), LocalEnd = Tomorrow.AddDays(3).AddHours(11),
            Attendees = 2, IdempotencyKey = Guid.NewGuid().ToString(),
        });
        var before = await CountBookingsAsync();

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.CreateSeriesAsync(Daily(s.Space.Id, 5)));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.SeriesDateUnavailable);
        (await CountBookingsAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Unticking_every_date_books_nothing()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.CreateSeriesAsync(Daily(s.Space.Id, 2, skip: new[] { 0, 1 })));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.SeriesNothingToBook);
    }

    [Fact]
    public async Task Retrying_a_series_with_the_same_key_returns_it_instead_of_booking_twice()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var request = Daily(s.Space.Id, 3);

        var first = await _bookingsAppService.CreateSeriesAsync(request);
        var again = await _bookingsAppService.CreateSeriesAsync(request);

        again.SeriesId.ShouldBe(first.SeriesId);
        again.Bookings.Select(b => b.Id).ShouldBe(first.Bookings.Select(b => b.Id));
        (await CountBookingsAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task Under_block_a_date_I_am_booked_elsewhere_fails_on_its_own()
    {
        var s = await CreateScenarioAsync();
        await SetPolicyAsync(s, OwnOverlapPolicy.Block);
        var desk = await AddSpaceAsync(s, "Desk 7");
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(new CreateBookingDto
        {
            SpaceId = desk.Id, LocalStart = Tomorrow.AddDays(1).AddHours(10), LocalEnd = Tomorrow.AddDays(1).AddHours(11),
            Attendees = 1, IdempotencyKey = Guid.NewGuid().ToString(),
        });

        var preview = await _bookingsAppService.PreviewSeriesAsync(Daily(s.Space.Id, 3));

        preview.Occurrences.Select(o => o.IsValid).ShouldBe(new[] { true, false, true });
        preview.Occurrences[1].Violations.ShouldHaveSingleItem().ShortMessage.ShouldBe("You're in Desk 7 then");
    }

    [Fact]
    public async Task Weekly_with_no_day_picked_is_refused()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var request = Daily(s.Space.Id, 7);
        request.Recurrence.Frequency = RecurrenceFrequency.Weekly;

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.PreviewSeriesAsync(request));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.SeriesNoWeekdays);
    }

    [Fact]
    public async Task Cancelling_this_and_following_leaves_the_earlier_dates()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var created = await _bookingsAppService.CreateSeriesAsync(Daily(s.Space.Id, 5));

        var cancelled = await _bookingsAppService.CancelAsync(
            created.Bookings[2].Id, new CancelBookingDto { Scope = CancelScope.ThisAndFollowing, Reason = "Project ended" });

        cancelled.Items.Select(b => b.Id).ShouldBe(created.Bookings.Skip(2).Select(b => b.Id));
        var left = await _bookingsAppService.GetMineAsync(Days(0, 5));
        left.Items.Select(b => b.Id).ShouldBe(created.Bookings.Take(2).Select(b => b.Id));
    }

    [Fact]
    public async Task Cancelling_the_series_cancels_every_upcoming_date_and_just_this_cancels_one()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var created = await _bookingsAppService.CreateSeriesAsync(Daily(s.Space.Id, 4));

        (await _bookingsAppService.CancelAsync(created.Bookings[1].Id, new CancelBookingDto())).Items.Count.ShouldBe(1);
        var rest = await _bookingsAppService.CancelAsync(created.Bookings[3].Id, new CancelBookingDto { Scope = CancelScope.Series });

        rest.Items.Select(b => b.Id).ShouldBe(new[] { created.Bookings[0].Id, created.Bookings[2].Id, created.Bookings[3].Id });
        (await _bookingsAppService.GetMineAsync(Days(0, 4))).Items.ShouldBeEmpty();
    }

    private static GetMyBookingsInput Days(int fromOffset, int toOffset) => new()
    {
        From = Tomorrow.AddDays(fromOffset),
        To = Tomorrow.AddDays(toOffset),
    };

    [Fact]
    public async Task Mine_lists_only_my_confirmed_bookings_in_the_range_earliest_first()
    {
        var s = await CreateScenarioAsync();
        var other = await CreateScenarioAsync();
        using (ActAs(other.UserId))
        {
            await _bookingsAppService.CreateAsync(Request(other.Space.Id, 9, 10));
        }

        using var _ = ActAs(s.UserId);
        var late = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 15, 16));
        var early = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 9, 10));
        var cancelled = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 12, 13));
        await _bookingsAppService.CancelAsync(cancelled.Id, new CancelBookingDto());
        var dayAfter = await _bookingsAppService.CreateAsync(new CreateBookingDto
        {
            SpaceId = s.Space.Id,
            LocalStart = Tomorrow.AddDays(1).AddHours(9),
            LocalEnd = Tomorrow.AddDays(1).AddHours(10),
            Attendees = 2,
            IdempotencyKey = Guid.NewGuid().ToString(),
        });

        var tomorrowOnly = await _bookingsAppService.GetMineAsync(Days(0, 1));
        tomorrowOnly.Items.Select(b => b.Id).ShouldBe(new[] { early.Id, late.Id });
        tomorrowOnly.Items[0].SpaceName.ShouldBe("Room 1");
        tomorrowOnly.Items[0].LocalStart.ShouldBe(Tomorrow.AddHours(9));

        var twoDays = await _bookingsAppService.GetMineAsync(Days(0, 2));
        twoDays.Items.Select(b => b.Id).ShouldBe(new[] { early.Id, late.Id, dayAfter.Id });
    }

    [Fact]
    public async Task Get_returns_my_own_booking_in_full_and_not_found_for_someone_elses()
    {
        var s = await CreateScenarioAsync();
        var other = await CreateScenarioAsync();
        Guid id;
        using (ActAs(s.UserId))
        {
            id = (await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11))).Id;

            var mine = await _bookingsAppService.GetAsync(id);
            mine.SpaceName.ShouldBe("Room 1");
            mine.FloorName.ShouldBe("Level 1");
            mine.Attendees.ShouldBe(2);
        }

        // Not "forbidden": a 403 would confirm the id exists.
        using (ActAs(other.UserId))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _bookingsAppService.GetAsync(id));
        }
    }

    [Fact]
    public async Task Mine_still_names_a_booking_whose_room_was_deleted()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));
        await _spaceRepository.DeleteAsync(s.Space.Id);

        var mine = await _bookingsAppService.GetMineAsync(Days(0, 1));

        mine.Items.ShouldHaveSingleItem().SpaceName.ShouldBe("Room 1");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(0, 63)]
    public async Task Mine_rejects_an_empty_backwards_or_too_long_range(int from, int to)
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.GetMineAsync(Days(from, to)));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingInvalidDateRange);
    }

    [Fact]
    public async Task Cancelling_frees_the_slot_and_keeps_who_and_why()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var booking = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));

        var result = await _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto { Reason = "  Meeting moved  " });

        result.Items.ShouldHaveSingleItem().Status.ShouldBe(nameof(BookingStatus.Cancelled));
        var stored = await _bookingRepository.GetAsync(booking.Id);
        stored.CancelledById.ShouldBe(s.UserId);
        stored.CancelReason.ShouldBe("Meeting moved");
        stored.CancelledByAdmin.ShouldBeFalse();

        var again = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));
        again.Status.ShouldBe(nameof(BookingStatus.Confirmed));
    }

    [Fact]
    public async Task Someone_else_cannot_cancel_my_booking()
    {
        var s = await CreateScenarioAsync();
        var other = await CreateScenarioAsync();
        BookingDto booking;
        using (ActAs(s.UserId))
        {
            booking = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));
        }

        using var _ = ActAs(other.UserId);
        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto()));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingNotYours);
        (await _bookingRepository.GetAsync(booking.Id)).Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task A_booking_cannot_be_cancelled_twice()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var booking = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11));
        await _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto());

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto()));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingNotCancellable);
    }

    [Fact]
    public async Task A_booking_that_has_started_cannot_be_cancelled()
    {
        var s = await CreateScenarioAsync();
        // Written straight to the table — the app service rightly refuses to book the past.
        var started = await WithUnitOfWorkAsync(() => _bookingRepository.InsertAsync(new Booking(
            Guid.NewGuid(), s.Space.Id, s.UserId,
            DateTimeOffset.UtcNow.AddMinutes(-30), DateTimeOffset.UtcNow.AddMinutes(30),
            attendees: 2, title: "Stand-up", resolvedConstraintsJson: "{}", idempotencyKey: Guid.NewGuid().ToString())));
        using var _ = ActAs(s.UserId);

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.CancelAsync(started.Id, new CancelBookingDto()));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingAlreadyStarted);
    }

    // ---- Edge cases added with the referential-integrity pass ----

    [Fact]
    public async Task A_weekly_rule_that_matches_no_date_is_refused_not_a_crash()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var weekly = Daily(s.Space.Id, 1);
        // Only the weekday after the start date, but the series ends on the start date itself.
        weekly.Recurrence = new RecurrenceDto
        {
            Frequency = RecurrenceFrequency.Weekly, Interval = 1,
            Weekdays = new[] { (int)Tomorrow.AddDays(1).DayOfWeek }, EndDate = Day(0),
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.PreviewSeriesAsync(weekly));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.SeriesNothingToBook);
    }

    [Fact]
    public async Task Replaying_a_key_whose_booking_was_cancelled_is_refused()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        var key = Guid.NewGuid().ToString();
        var created = await _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11, key: key));
        await _bookingsAppService.CancelAsync(created.Id, new CancelBookingDto());

        var ex = await Should.ThrowAsync<BusinessException>(() => _bookingsAppService.CreateAsync(Request(s.Space.Id, 10, 11, key: key)));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingIdempotencyKeyReused);
        (await CountBookingsAsync()).ShouldBe(1);
    }
}
