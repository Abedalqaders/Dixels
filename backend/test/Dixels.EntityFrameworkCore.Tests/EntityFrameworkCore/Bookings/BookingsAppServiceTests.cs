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
            Guid.NewGuid(), "Test HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));

        var floor = await _floorRepository.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "Level 1", 1));

        var spaceType = await _spaceTypeRepository.FirstAsync();
        var space = new Space(Guid.NewGuid(), floor.Id, "Room 1", spaceType.Id, capacity: 8);
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
    public async Task A_blank_title_falls_back_to_the_default()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var request = Request(s.Space.Id, 10, 11);
        request.Title = "   ";

        (await _bookingsAppService.CreateAsync(request)).Title.ShouldBe(BookingConsts.DefaultTitle);
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

    [Fact]
    public void The_employee_role_is_seeded_with_the_real_booking_permission_names()
    {
        RoleDataSeedContributor.EmployeePermissions.ShouldBe(new[]
        {
            DixelsPermissions.Bookings.Default,
            DixelsPermissions.Bookings.Create,
        });
    }
}
