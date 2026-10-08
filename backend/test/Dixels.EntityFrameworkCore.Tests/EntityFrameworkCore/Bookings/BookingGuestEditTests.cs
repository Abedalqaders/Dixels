using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// The owner changing who's invited after booking: the same guest rules as a new booking,
/// only the head-count room rules (and those grandfathered), answers kept for guests who
/// stay, a series changed as a whole, and one event naming who was added and removed.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingGuestEditTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookings;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    public BookingGuestEditTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    /// <summary>A UTC, 24/7 building with a 6-seat room; the owner and three colleagues work there, plus a stranger.</summary>
    private sealed record Scenario(Guid SpaceId, Guid Owner, Guid Rana, Guid Omar, Guid Lina, Guid Stranger);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await _spaceRepository.InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, capacity: 6));

        async Task<Guid> NewUserAsync(string name)
        {
            var key = Guid.NewGuid().ToString("N")[..8];
            var user = new IdentityUser(Guid.NewGuid(), "u" + key, $"{name.ToLowerInvariant()}.{key}@test.io") { Name = name };
            user.SetBuildingId(building.Id);
            (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return user.Id;
        }

        return new Scenario(space.Id, await NewUserAsync("Owner"), await NewUserAsync("Rana"), await NewUserAsync("Omar"), await NewUserAsync("Lina"), await NewUserAsync("Stranger"));
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private static InviteeDto Colleague(Guid id) => new() { UserId = id };

    private static UpdateInviteesDto Guests(int attendees, params Guid[] colleagues) =>
        new() { Attendees = attendees, Invitees = colleagues.Select(Colleague).ToList() };

    private Task<BookingDto> BookAsync(Scenario s, int attendees, params Guid[] colleagues) => _bookings.CreateAsync(new CreateBookingDto
    {
        SpaceId = s.SpaceId,
        LocalStart = Tomorrow.AddHours(10),
        LocalEnd = Tomorrow.AddHours(11),
        Attendees = attendees,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Invitees = colleagues.Select(Colleague).ToList(),
    });

    private Task<SeriesCreatedDto> BookSeriesAsync(Scenario s, int days, int attendees, params Guid[] colleagues) => _bookings.CreateSeriesAsync(new CreateSeriesDto
    {
        SpaceId = s.SpaceId,
        LocalStart = Tomorrow.AddHours(10),
        LocalEnd = Tomorrow.AddHours(11),
        Attendees = attendees,
        Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(days - 1)) },
        IdempotencyKey = Guid.NewGuid().ToString(),
        Invitees = colleagues.Select(Colleague).ToList(),
    });

    private async Task<string> RejectionCodeAsync(Func<Task> act) => (await Should.ThrowAsync<BusinessException>(act)).Code!;

    private Task ChangeRoomAsync(Guid spaceId, Action<Space> change) => WithUnitOfWorkAsync(async () =>
    {
        var space = await _spaceRepository.GetAsync(spaceId);
        change(space);
        await _spaceRepository.UpdateAsync(space);
    });

    /// <summary>Sets a guest's answer directly (answering arrives in a later task).</summary>
    private Task AnswerAsync(Guid bookingId, Guid userId, InviteeResponseStatus status) => WithUnitOfWorkAsync(async () =>
    {
        var dbContext = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
        var row = await dbContext.Set<BookingAttendee>().SingleAsync(a => a.BookingId == bookingId && a.UserId == userId);
        dbContext.Entry(row).Property(nameof(BookingAttendee.ResponseStatus)).CurrentValue = status;
        await dbContext.SaveChangesAsync();
    });

    // ---- One booking ----

    [Fact]
    public async Task The_owner_adds_and_removes_guests_and_one_event_names_who()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var booking = await BookAsync(s, 3, s.Rana, s.Omar);
        BookingInviteesChangedEvent? heard = null;

        BookingDto updated;
        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingInviteesChangedEvent>(e => { heard = e; return Task.CompletedTask; }))
        {
            updated = await _bookings.UpdateInviteesAsync(booking.Id, Guests(4, s.Rana, s.Lina));
        }

        updated.Attendees.ShouldBe(4);
        updated.Invitees.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Rana, s.Lina }, ignoreOrder: true);
        (await _bookings.GetAsync(booking.Id)).Invitees.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Rana, s.Lina }, ignoreOrder: true);

        heard.ShouldNotBeNull();
        heard.BookingId.ShouldBe(booking.Id);
        heard.SeriesId.ShouldBeNull();
        heard.Added.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Lina });
        heard.Removed.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Omar });
    }

    [Fact]
    public async Task A_guest_who_stays_keeps_their_answer_and_a_new_one_starts_pending()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var booking = await BookAsync(s, 2, s.Rana);
        await AnswerAsync(booking.Id, s.Rana, InviteeResponseStatus.Accepted);

        var updated = await _bookings.UpdateInviteesAsync(booking.Id, Guests(3, s.Rana, s.Omar));

        updated.Invitees.Single(i => i.UserId == s.Rana).ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
        updated.Invitees.Single(i => i.UserId == s.Omar).ResponseStatus.ShouldBe(InviteeResponseStatus.Pending);
    }

    [Fact]
    public async Task Changing_only_the_head_count_raises_no_event()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var booking = await BookAsync(s, 2, s.Rana);
        var heard = false;

        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingInviteesChangedEvent>(_ => { heard = true; return Task.CompletedTask; }))
        {
            (await _bookings.UpdateInviteesAsync(booking.Id, Guests(5, s.Rana))).Attendees.ShouldBe(5);
        }

        heard.ShouldBeFalse();
    }

    [Fact]
    public async Task The_guest_rules_of_a_new_booking_apply()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var booking = await BookAsync(s, 2);

        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(3, s.Rana, s.Rana))))
            .ShouldBe(DixelsDomainErrorCodes.InviteeDuplicate);
        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(2, s.Owner))))
            .ShouldBe(DixelsDomainErrorCodes.InviteeIsOwner);
        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(2, Guid.NewGuid()))))
            .ShouldBe(DixelsDomainErrorCodes.InviteeNotInBuilding);
        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(2, s.Rana, s.Omar))))
            .ShouldBe(DixelsDomainErrorCodes.BookingAttendeesBelowInvitees);
        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(7, s.Rana))))
            .ShouldBe(DixelsDomainErrorCodes.BookingOverCapacity);
    }

    [Fact]
    public async Task A_booking_kept_over_a_shrunk_room_can_still_lose_guests_but_not_grow()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var booking = await BookAsync(s, 6, s.Rana, s.Omar);
        await ChangeRoomAsync(s.SpaceId, space => space.SetCapacity(4));

        // Same head count, one guest fewer: allowed although 6 > the room's 4 now.
        (await _bookings.UpdateInviteesAsync(booking.Id, Guests(6, s.Rana))).Invitees.Count.ShouldBe(1);
        // Lower but still over: allowed too.
        (await _bookings.UpdateInviteesAsync(booking.Id, Guests(5, s.Rana))).Attendees.ShouldBe(5);
        // Higher: the room's current size applies.
        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(6, s.Rana))))
            .ShouldBe(DixelsDomainErrorCodes.BookingOverCapacity);
    }

    [Fact]
    public async Task A_booking_under_a_raised_minimum_can_still_gain_guests_but_not_shrink()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var booking = await BookAsync(s, 3, s.Rana);
        await ChangeRoomAsync(s.SpaceId, space => space.SetMinAttendees(5));

        // Same head count, another guest: allowed although 3 < the room's new minimum of 5.
        (await _bookings.UpdateInviteesAsync(booking.Id, Guests(3, s.Rana, s.Omar))).Invitees.Count.ShouldBe(2);
        // Lower: the room's current minimum applies.
        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(2, s.Rana))))
            .ShouldBe(DixelsDomainErrorCodes.BookingBelowMinAttendees);
    }

    [Fact]
    public async Task Only_the_organiser_may_edit_a_guest_is_told_so_and_anyone_else_finds_nothing()
    {
        var s = await CreateScenarioAsync();
        BookingDto booking;
        using (ActAs(s.Owner))
        {
            booking = await BookAsync(s, 2, s.Rana);
        }

        using (ActAs(s.Rana))
        {
            (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(2))))
                .ShouldBe(DixelsDomainErrorCodes.BookingOrganiserOnly);
        }

        using (ActAs(s.Stranger))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _bookings.UpdateInviteesAsync(booking.Id, Guests(2)));
        }
    }

    [Fact]
    public async Task A_cancelled_or_started_booking_keeps_its_guests()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var cancelled = await BookAsync(s, 2, s.Rana);
        await _bookings.CancelAsync(cancelled.Id, new CancelBookingDto());

        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(cancelled.Id, Guests(2))))
            .ShouldBe(DixelsDomainErrorCodes.GuestsNotEditable);

        // One that already started: written straight in, since a booking can't be made in the past.
        var started = await WithUnitOfWorkAsync(() => _bookingRepository.InsertAsync(new Booking(
            Guid.NewGuid(), s.SpaceId, s.Owner, DateTimeOffset.UtcNow.AddMinutes(-30), DateTimeOffset.UtcNow.AddMinutes(30),
            attendees: 1, "Now", "{}", Guid.NewGuid().ToString())));
        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(started.Id, Guests(2, s.Rana))))
            .ShouldBe(DixelsDomainErrorCodes.GuestsNotEditable);
    }

    // ---- A series ----

    [Fact]
    public async Task A_date_of_a_series_is_changed_only_with_its_series()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var series = await BookSeriesAsync(s, 2, 2, s.Rana);

        (await RejectionCodeAsync(() => _bookings.UpdateInviteesAsync(series.Bookings[0].Id, Guests(2, s.Omar))))
            .ShouldBe(DixelsDomainErrorCodes.EditSeriesGuests);
    }

    [Fact]
    public async Task A_series_edit_changes_the_series_and_its_upcoming_dates_but_not_cancelled_ones()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        var series = await BookSeriesAsync(s, 3, 2, s.Rana);
        await _bookings.CancelAsync(series.Bookings[2].Id, new CancelBookingDto());
        BookingInviteesChangedEvent? heard = null;

        SeriesCreatedDto updated;
        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingInviteesChangedEvent>(e => { heard = e; return Task.CompletedTask; }))
        {
            updated = await _bookings.UpdateSeriesInviteesAsync(series.SeriesId, Guests(3, s.Omar, s.Lina));
        }

        updated.Bookings.Select(b => b.Id).ShouldBe(new[] { series.Bookings[0].Id, series.Bookings[1].Id });
        updated.Bookings.ShouldAllBe(b => b.Attendees == 3 && b.Invitees.Count == 2);

        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetListAsync(b => b.SeriesId == series.SeriesId, includeDetails: true));
        stored.Single(b => b.Id == series.Bookings[2].Id).Invitees.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Rana });

        var seriesRow = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookingSeries, Guid>>().GetAsync(series.SeriesId));
        seriesRow.Attendees.ShouldBe(3);
        seriesRow.Invitees.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Omar, s.Lina }, ignoreOrder: true);

        heard.ShouldNotBeNull();
        heard.SeriesId.ShouldBe(series.SeriesId);
        heard.BookingId.ShouldBeNull();
        heard.Bookings.Count.ShouldBe(2);
        heard.Added.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Omar, s.Lina }, ignoreOrder: true);
        heard.Removed.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Rana });
    }

    [Fact]
    public async Task A_series_with_nothing_upcoming_cannot_be_edited_its_guests_are_refused_and_strangers_find_nothing()
    {
        var s = await CreateScenarioAsync();
        SeriesCreatedDto series;
        using (ActAs(s.Owner))
        {
            series = await BookSeriesAsync(s, 2, 2, s.Rana);
        }

        using (ActAs(s.Stranger))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _bookings.UpdateSeriesInviteesAsync(series.SeriesId, Guests(2)));
        }

        using (ActAs(s.Rana))
        {
            (await RejectionCodeAsync(() => _bookings.UpdateSeriesInviteesAsync(series.SeriesId, Guests(2))))
                .ShouldBe(DixelsDomainErrorCodes.BookingOrganiserOnly);
        }

        using (ActAs(s.Owner))
        {
            await _bookings.CancelAsync(series.Bookings[0].Id, new CancelBookingDto { Scope = CancelScope.Series });
            (await RejectionCodeAsync(() => _bookings.UpdateSeriesInviteesAsync(series.SeriesId, Guests(2, s.Omar))))
                .ShouldBe(DixelsDomainErrorCodes.GuestsNotEditable);
        }
    }

    [Fact]
    public async Task The_details_carry_the_rooms_size_for_checking_an_edit()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner);
        await ChangeRoomAsync(s.SpaceId, space => space.SetMinAttendees(2));

        var booking = await BookAsync(s, 2, s.Rana);

        booking.Capacity.ShouldBe(6);
        booking.MinAttendees.ShouldBe(2);
    }
}
