using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Settings;
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
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Volo.Abp.Validation;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// Inviting people to a booking: who may be invited (colleagues of the room's building, and
/// outsiders by email when that's switched on), what the guest list must look like, that the
/// head count leaves room for everyone, and that a series gives every date the same guests.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingInviteesTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookings;
    private readonly IColleaguesAppService _colleagues;
    private readonly IBookingRepository _bookingRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    public BookingInviteesTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _colleagues = GetRequiredService<IColleaguesAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Person(Guid Id, string Email);

    /// <summary>
    /// A UTC, 24/7 building with an 8-seat room; the owner and two colleagues work there, one
    /// person works elsewhere and one is deactivated.
    /// </summary>
    private sealed record Scenario(Guid SpaceId, Person Owner, Person Rana, Person Omar, Person Elsewhere, Person Inactive);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var buildings = GetRequiredService<IRepository<Building, Guid>>();
        Building NewBuilding() => new(
            Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0);
        var building = await buildings.InsertAsync(NewBuilding());
        var other = await buildings.InsertAsync(NewBuilding());

        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, capacity: 8));

        async Task<Person> NewUserAsync(string name, Guid buildingId, bool active = true)
        {
            var key = Guid.NewGuid().ToString("N")[..8];
            var user = new IdentityUser(Guid.NewGuid(), "u" + key, $"{name.ToLowerInvariant()}.{key}@test.io") { Name = name, Surname = "Test" };
            user.SetBuildingId(buildingId);
            user.SetIsActive(active);
            (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return new Person(user.Id, user.Email);
        }

        return new Scenario(
            space.Id,
            await NewUserAsync("Owner", building.Id),
            await NewUserAsync("Rana", building.Id),
            await NewUserAsync("Omar", building.Id),
            await NewUserAsync("Elsewhere", other.Id),
            await NewUserAsync("Inactive", building.Id, active: false));
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private Task AllowExternalGuestsAsync() => WithUnitOfWorkAsync(() =>
        GetRequiredService<ISettingManager>().SetGlobalAsync(DixelsSettings.ExternalGuestsEnabled, "true"));

    private static InviteeDto Colleague(Person p) => new() { UserId = p.Id };

    private static InviteeDto ByEmail(string email, string? name = null) => new() { Email = email, Name = name };

    private static CreateBookingDto Request(Guid spaceId, int attendees, params InviteeDto[] invitees) => new()
    {
        SpaceId = spaceId,
        LocalStart = Tomorrow.AddHours(10),
        LocalEnd = Tomorrow.AddHours(11),
        Title = "Planning",
        IdempotencyKey = Guid.NewGuid().ToString(),
        Invitees = invitees.ToList(),
    };

    private async Task<string> RejectionCodeAsync(Func<Task> act) => (await Should.ThrowAsync<BusinessException>(act)).Code!;

    // ---- Saving and showing ----

    [Fact]
    public async Task A_booking_saves_its_invited_colleagues_and_shows_them_to_the_owner()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);

        var created = await _bookings.CreateAsync(Request(s.SpaceId, 3, Colleague(s.Rana), Colleague(s.Omar)));
        var read = await _bookings.GetAsync(created.Id);

        read.IsOwner.ShouldBeTrue();
        read.OwnerName.ShouldBe("Owner Test");
        read.Invitees.Select(i => (i.UserId, i.Name, i.Email, i.IsExternal, i.ResponseStatus)).ShouldBe(new[]
        {
            ((Guid?)s.Rana.Id, "Rana Test", s.Rana.Email, false, InviteeResponseStatus.Pending),
            ((Guid?)s.Omar.Id, "Omar Test", s.Omar.Email, false, InviteeResponseStatus.Pending),
        }, ignoreOrder: true);
        created.Invitees.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_typed_email_of_a_colleague_becomes_that_colleague_in_the_preview_and_when_saved()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);
        var typed = "  " + s.Rana.Email.ToUpperInvariant() + " ";

        var preview = await _bookings.PreviewAsync(Request(s.SpaceId, 2, ByEmail(typed, "whoever")));
        var created = await _bookings.CreateAsync(Request(s.SpaceId, 2, ByEmail(typed, "whoever")));

        foreach (var invitee in new[] { preview.Invitees.ShouldHaveSingleItem(), created.Invitees.ShouldHaveSingleItem() })
        {
            invitee.UserId.ShouldBe(s.Rana.Id);
            invitee.IsExternal.ShouldBeFalse();
            invitee.Name.ShouldBe("Rana Test");
        }
    }

    [Fact]
    public async Task External_guests_are_allowed_by_default_now_that_they_get_an_invite()
    {
        (await GetRequiredService<ISettingDefinitionManager>().GetAsync(DixelsSettings.ExternalGuestsEnabled)).DefaultValue.ShouldBe("true");
    }

    [Fact]
    public async Task External_guests_are_refused_while_the_switch_is_off()
    {
        var s = await CreateScenarioAsync();
        await WithUnitOfWorkAsync(() =>
            GetRequiredService<ISettingManager>().SetGlobalAsync(DixelsSettings.ExternalGuestsEnabled, "false"));
        using var _ = ActAs(s.Owner.Id);

        (await RejectionCodeAsync(() => _bookings.PreviewAsync(Request(s.SpaceId, 2, ByEmail("guest@outside.io")))))
            .ShouldBe(DixelsDomainErrorCodes.ExternalGuestsDisabled);
    }

    [Fact]
    public async Task With_the_switch_on_an_external_guest_is_saved_by_email_and_name()
    {
        var s = await CreateScenarioAsync();
        await AllowExternalGuestsAsync();
        using var _ = ActAs(s.Owner.Id);

        var created = await _bookings.CreateAsync(Request(s.SpaceId, 2, ByEmail(" guest@outside.io ", " Sara Guest ")));

        var guest = created.Invitees.ShouldHaveSingleItem();
        guest.ShouldSatisfyAllConditions(
            () => guest.UserId.ShouldBeNull(),
            () => guest.IsExternal.ShouldBeTrue(),
            () => guest.Email.ShouldBe("guest@outside.io"),
            () => guest.Name.ShouldBe("Sara Guest"));
    }

    // ---- What the list may contain ----

    [Fact]
    public async Task Only_active_colleagues_of_the_rooms_building_can_be_invited()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);

        foreach (var who in new[] { s.Elsewhere.Id, s.Inactive.Id, Guid.NewGuid() })
        {
            (await RejectionCodeAsync(() => _bookings.PreviewAsync(Request(s.SpaceId, 2, new InviteeDto { UserId = who }))))
                .ShouldBe(DixelsDomainErrorCodes.InviteeNotInBuilding);
        }
    }

    [Fact]
    public async Task A_typed_email_of_someone_in_another_building_stays_an_external_guest()
    {
        var s = await CreateScenarioAsync();
        await AllowExternalGuestsAsync();
        using var _ = ActAs(s.Owner.Id);

        var preview = await _bookings.PreviewAsync(Request(s.SpaceId, 2, ByEmail(s.Elsewhere.Email)));

        preview.Invitees.ShouldHaveSingleItem().IsExternal.ShouldBeTrue();
    }

    [Fact]
    public async Task The_same_person_twice_is_refused_however_they_were_added()
    {
        var s = await CreateScenarioAsync();
        await AllowExternalGuestsAsync();
        using var _ = ActAs(s.Owner.Id);

        var twice = new[]
        {
            new[] { Colleague(s.Rana), Colleague(s.Rana) },
            new[] { Colleague(s.Rana), ByEmail(s.Rana.Email.ToUpperInvariant()) },
            new[] { ByEmail("guest@outside.io"), ByEmail(" GUEST@outside.io") },
        };

        foreach (var list in twice)
        {
            (await RejectionCodeAsync(() => _bookings.PreviewAsync(Request(s.SpaceId, 3, list))))
                .ShouldBe(DixelsDomainErrorCodes.InviteeDuplicate);
        }
    }

    [Fact]
    public async Task The_owner_cannot_invite_themselves_by_id_or_by_email()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);

        (await RejectionCodeAsync(() => _bookings.PreviewAsync(Request(s.SpaceId, 2, Colleague(s.Owner)))))
            .ShouldBe(DixelsDomainErrorCodes.InviteeIsOwner);
        (await RejectionCodeAsync(() => _bookings.PreviewAsync(Request(s.SpaceId, 2, ByEmail(s.Owner.Email)))))
            .ShouldBe(DixelsDomainErrorCodes.InviteeIsOwner);
    }

    [Fact]
    public async Task Each_invitee_is_a_colleague_or_an_email_not_both_or_neither()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);

        foreach (var invitee in new[] { new InviteeDto { UserId = s.Rana.Id, Email = s.Rana.Email }, new InviteeDto { Name = "Nobody" } })
        {
            (await RejectionCodeAsync(() => _bookings.PreviewAsync(Request(s.SpaceId, 2, invitee))))
                .ShouldBe(DixelsDomainErrorCodes.InviteeInvalid);
        }
    }

    // ---- Retries ----

    [Fact]
    public async Task A_retry_must_ask_for_the_same_guests()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);
        var first = Request(s.SpaceId, 3, Colleague(s.Rana), Colleague(s.Omar));
        var created = await _bookings.CreateAsync(first);

        // Same people in another order (one typed by email) is the same request.
        var again = Request(s.SpaceId, 3, Colleague(s.Omar), ByEmail(s.Rana.Email));
        again.IdempotencyKey = first.IdempotencyKey;
        (await _bookings.CreateAsync(again)).Id.ShouldBe(created.Id);

        var different = Request(s.SpaceId, 3, Colleague(s.Rana));
        different.IdempotencyKey = first.IdempotencyKey;
        (await RejectionCodeAsync(() => _bookings.CreateAsync(different))).ShouldBe(DixelsDomainErrorCodes.BookingIdempotencyKeyReused);
    }

    // ---- Series ----

    private static CreateSeriesDto Daily(Guid spaceId, int days, int attendees, params InviteeDto[] invitees) => new()
    {
        SpaceId = spaceId,
        LocalStart = Tomorrow.AddHours(10),
        LocalEnd = Tomorrow.AddHours(11),
        Title = "Stand-up",
        Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(days - 1)) },
        IdempotencyKey = Guid.NewGuid().ToString(),
        Invitees = invitees.ToList(),
    };

    [Fact]
    public async Task A_series_keeps_its_guest_list_and_gives_every_date_a_copy_in_one_event()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);
        BookingSeriesConfirmedEvent? heard = null;

        SeriesCreatedDto created;
        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingSeriesConfirmedEvent>(e => { heard = e; return Task.CompletedTask; }))
        {
            created = await _bookings.CreateSeriesAsync(Daily(s.SpaceId, 3, 3, Colleague(s.Rana), Colleague(s.Omar)));
        }

        created.Bookings.Count.ShouldBe(3);
        created.Bookings.ShouldAllBe(b => b.Invitees.Count == 2);

        heard.ShouldNotBeNull();
        heard.Series.Invitees.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Rana.Id, s.Omar.Id }, ignoreOrder: true);
        heard.Bookings.ShouldAllBe(b => b.Invitees.Count == 2);

        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetListAsync(b => b.SeriesId == created.SeriesId, includeDetails: true));
        stored.ShouldAllBe(b => b.Invitees.Count == 2 && b.Invitees.All(i => i.EndsAt == b.EndsAt));
    }

    [Fact]
    public async Task A_series_retry_with_other_guests_is_refused()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);
        var first = Daily(s.SpaceId, 2, 2, Colleague(s.Rana));
        await _bookings.CreateSeriesAsync(first);

        var other = Daily(s.SpaceId, 2, 2, Colleague(s.Omar));
        other.IdempotencyKey = first.IdempotencyKey;

        (await RejectionCodeAsync(() => _bookings.CreateSeriesAsync(other))).ShouldBe(DixelsDomainErrorCodes.BookingIdempotencyKeyReused);
    }

    // ---- Events ----

    [Fact]
    public async Task The_confirmed_event_carries_the_guests_and_the_cancelled_event_a_copy_of_them()
    {
        var s = await CreateScenarioAsync();
        await AllowExternalGuestsAsync();
        using var _ = ActAs(s.Owner.Id);
        var bus = GetRequiredService<ILocalEventBus>();
        BookingConfirmedEvent? confirmed = null;
        BookingsCancelledEvent? cancelled = null;

        BookingDto created;
        using (bus.Subscribe<BookingConfirmedEvent>(e => { confirmed = e; return Task.CompletedTask; }))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 3, Colleague(s.Rana), ByEmail("guest@outside.io", "Sara")));
        }

        using (bus.Subscribe<BookingsCancelledEvent>(e => { cancelled = e; return Task.CompletedTask; }))
        {
            await _bookings.CancelAsync(created.Id, new CancelBookingDto());
        }

        confirmed.ShouldNotBeNull().Booking.Invitees.Count.ShouldBe(2);
        cancelled.ShouldNotBeNull().InviteesByBooking[created.Id]
            .Select(i => (i.UserId, i.Email, i.Name))
            .ShouldBe(new[] { ((Guid?)s.Rana.Id, (string?)null, (string?)null), (null, "guest@outside.io", "Sara") }, ignoreOrder: true);
    }

    [Fact]
    public async Task An_admin_cancel_by_ids_carries_a_copy_of_the_guests_too()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 2, Colleague(s.Rana)));
        }

        BookingsCancelledEvent? cancelled = null;
        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingsCancelledEvent>(e => { cancelled = e; return Task.CompletedTask; }))
        {
            await WithUnitOfWorkAsync(() => GetRequiredService<BookingImpactChecker>()
                .CancelUpcomingAsAdminAsync(new[] { created.Id }, Guid.NewGuid(), "The room was removed"));
        }

        cancelled.ShouldNotBeNull().ByAdmin.ShouldBeTrue();
        cancelled.InviteesByBooking[created.Id].ShouldHaveSingleItem().UserId.ShouldBe(s.Rana.Id);
    }

    // ---- My calendar and reading an invite (T3) ----

    private async Task<List<BookingSummaryDto>> CalendarOfAsync(Person p)
    {
        using var _ = ActAs(p.Id);
        return (await _bookings.GetMineAsync(new GetMyBookingsInput { From = Tomorrow, To = Tomorrow.AddDays(1) })).Items.ToList();
    }

    [Fact]
    public async Task An_invited_colleague_sees_the_booking_in_their_calendar_marked_as_an_invite()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 2, Colleague(s.Rana)));
        }

        (await CalendarOfAsync(s.Rana)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            b => b.Id.ShouldBe(created.Id),
            b => b.IsInvited.ShouldBeTrue(),
            b => b.Title.ShouldBe("Planning"));
        (await CalendarOfAsync(s.Owner)).ShouldHaveSingleItem().IsInvited.ShouldBeFalse();
        (await CalendarOfAsync(s.Omar)).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_invite_the_owner_cancelled_leaves_the_calendar_but_one_an_admin_cancelled_stays_struck_through()
    {
        var s = await CreateScenarioAsync();
        BookingDto byOwner, byAdmin;
        using (ActAs(s.Owner.Id))
        {
            byOwner = await _bookings.CreateAsync(Request(s.SpaceId, 2, Colleague(s.Rana)));
            var later = Request(s.SpaceId, 2, Colleague(s.Rana));
            later.LocalStart = Tomorrow.AddHours(12);
            later.LocalEnd = Tomorrow.AddHours(13);
            byAdmin = await _bookings.CreateAsync(later);
            await _bookings.CancelAsync(byOwner.Id, new CancelBookingDto());
        }
        await WithUnitOfWorkAsync(() => GetRequiredService<BookingImpactChecker>()
            .CancelUpcomingAsAdminAsync(new[] { byAdmin.Id }, Guid.NewGuid(), "The room was removed"));

        var shown = (await CalendarOfAsync(s.Rana)).ShouldHaveSingleItem();
        shown.Id.ShouldBe(byAdmin.Id);
        shown.Status.ShouldBe(nameof(BookingStatus.Cancelled));
        shown.IsInvited.ShouldBeTrue();
    }

    [Fact]
    public async Task A_colleague_guest_can_open_the_booking_read_only_and_sees_other_guests_by_name_only()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 3, Colleague(s.Rana), Colleague(s.Omar)));
        }

        using var _ = ActAs(s.Rana.Id);
        var read = await _bookings.GetAsync(created.Id);

        read.IsOwner.ShouldBeFalse();
        read.OwnerName.ShouldBe("Owner Test");
        read.Invitees.Select(i => i.Name).ShouldBe(new[] { "Rana Test", "Omar Test" }, ignoreOrder: true);
        read.Invitees.ShouldAllBe(i => i.Email == string.Empty);
    }

    [Fact]
    public async Task A_guest_is_told_only_the_organiser_cancels_and_a_stranger_cannot_even_open_it()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 2, Colleague(s.Rana)));
        }

        using (ActAs(s.Rana.Id))
        {
            (await RejectionCodeAsync(() => _bookings.CancelAsync(created.Id, new CancelBookingDto())))
                .ShouldBe(DixelsDomainErrorCodes.BookingOnlyOrganiserCancels);
        }
        using (ActAs(s.Omar.Id))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _bookings.CancelAsync(created.Id, new CancelBookingDto()));
        }
        using (ActAs(s.Omar.Id))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _bookings.GetAsync(created.Id));
        }

        (await _bookingRepository.GetAsync(created.Id)).Status.ShouldBe(BookingStatus.Confirmed);
    }

    // ---- Answering an invitation (T5) ----

    private static RespondToInviteDto Answer(InviteeResponseStatus status) => new() { Status = status };

    [Fact]
    public async Task A_guest_accepts_then_declines_and_everyone_invited_sees_the_answer()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 3, Colleague(s.Rana), Colleague(s.Omar)));
        }

        var answered = new List<BookingInviteeRespondedEvent>();
        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingInviteeRespondedEvent>(e => { answered.Add(e); return Task.CompletedTask; }))
        using (ActAs(s.Rana.Id))
        {
            (await _bookings.RespondAsync(created.Id, Answer(InviteeResponseStatus.Accepted))).MyResponse.ShouldBe(InviteeResponseStatus.Accepted);
            (await _bookings.RespondAsync(created.Id, Answer(InviteeResponseStatus.Declined))).MyResponse.ShouldBe(InviteeResponseStatus.Declined);
        }

        answered.Select(e => (e.BookingId, e.SeriesId, e.Guest.UserId, e.Status, e.PreviousStatus)).ShouldBe(new (Guid?, Guid?, Guid?, InviteeResponseStatus, InviteeResponseStatus)[]
        {
            (created.Id, null, s.Rana.Id, InviteeResponseStatus.Accepted, InviteeResponseStatus.Pending),
            (created.Id, null, s.Rana.Id, InviteeResponseStatus.Declined, InviteeResponseStatus.Accepted),
        });

        // The owner and the other guest both see Rana's answer; the owner has none of their own.
        BookingDto byOwner, byOmar;
        using (ActAs(s.Owner.Id)) { byOwner = await _bookings.GetAsync(created.Id); }
        using (ActAs(s.Omar.Id)) { byOmar = await _bookings.GetAsync(created.Id); }
        byOwner.MyResponse.ShouldBeNull();
        foreach (var read in new[] { byOwner, byOmar })
        {
            read.Invitees.Single(i => i.UserId == s.Rana.Id).ResponseStatus.ShouldBe(InviteeResponseStatus.Declined);
            read.Invitees.Single(i => i.UserId == s.Omar.Id).ResponseStatus.ShouldBe(InviteeResponseStatus.Pending);
        }
        byOmar.MyResponse.ShouldBe(InviteeResponseStatus.Pending);

        // My calendar carries my answer (a declined invite is drawn faded); my own bookings have none.
        (await CalendarOfAsync(s.Rana)).ShouldHaveSingleItem().MyResponse.ShouldBe(InviteeResponseStatus.Declined);
        (await CalendarOfAsync(s.Owner)).ShouldHaveSingleItem().MyResponse.ShouldBeNull();
    }

    [Fact]
    public async Task The_organiser_has_nothing_to_answer_and_a_stranger_is_told_it_does_not_exist()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 2, Colleague(s.Rana)));
            (await RejectionCodeAsync(() => _bookings.RespondAsync(created.Id, Answer(InviteeResponseStatus.Accepted))))
                .ShouldBe(DixelsDomainErrorCodes.BookingOrganiserCannotRespond);
        }

        using (ActAs(s.Omar.Id))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _bookings.RespondAsync(created.Id, Answer(InviteeResponseStatus.Accepted)));
        }
    }

    [Fact]
    public async Task Answers_close_once_the_booking_is_cancelled()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 2, Colleague(s.Rana)));
        }
        await WithUnitOfWorkAsync(() => GetRequiredService<BookingImpactChecker>()
            .CancelUpcomingAsAdminAsync(new[] { created.Id }, Guid.NewGuid(), "The room was removed"));

        using var _ = ActAs(s.Rana.Id);
        (await RejectionCodeAsync(() => _bookings.RespondAsync(created.Id, Answer(InviteeResponseStatus.Declined))))
            .ShouldBe(DixelsDomainErrorCodes.BookingResponseClosed);
    }

    [Fact]
    public async Task Pending_is_not_an_answer()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, 2, Colleague(s.Rana)));
        }

        using var _ = ActAs(s.Rana.Id);
        await Should.ThrowAsync<AbpValidationException>(() => _bookings.RespondAsync(created.Id, Answer(InviteeResponseStatus.Pending)));
    }

    [Fact]
    public async Task A_whole_series_answer_sets_every_upcoming_date_and_overwrites_single_date_answers()
    {
        var s = await CreateScenarioAsync();
        SeriesCreatedDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateSeriesAsync(Daily(s.SpaceId, 3, 2, Colleague(s.Rana)));
        }
        var dates = created.Bookings.OrderBy(b => b.StartsAt).Select(b => b.Id).ToList();

        var answered = new List<BookingInviteeRespondedEvent>();
        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingInviteeRespondedEvent>(e => { answered.Add(e); return Task.CompletedTask; }))
        using (ActAs(s.Rana.Id))
        {
            // One date first, then the whole series: the series answer wins everywhere.
            await _bookings.RespondAsync(dates[1], Answer(InviteeResponseStatus.Declined));
            await _bookings.RespondToSeriesAsync(created.SeriesId, Answer(InviteeResponseStatus.Accepted));
            (await _bookings.GetAsync(dates[1])).MyResponse.ShouldBe(InviteeResponseStatus.Accepted);

            // A single date can still differ afterwards.
            await _bookings.RespondAsync(dates[2], Answer(InviteeResponseStatus.Declined));
        }

        answered.Last().ShouldSatisfyAllConditions(
            e => e.BookingId.ShouldBe(dates[2]),
            e => e.SeriesId.ShouldBeNull());
        answered[1].ShouldSatisfyAllConditions(
            e => e.BookingId.ShouldBeNull(),
            e => e.SeriesId.ShouldBe(created.SeriesId),
            e => e.Status.ShouldBe(InviteeResponseStatus.Accepted));

        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetListAsync(b => b.SeriesId == created.SeriesId, includeDetails: true));
        stored.OrderBy(b => b.StartsAt).Select(b => b.Invitees.Single().ResponseStatus).ShouldBe(new[]
        {
            InviteeResponseStatus.Accepted, InviteeResponseStatus.Accepted, InviteeResponseStatus.Declined,
        });
        var series = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookingSeries, Guid>>().GetAsync(created.SeriesId));
        series.Invitees.Single().ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
    }

    [Fact]
    public async Task Only_a_series_guest_answers_for_the_series()
    {
        var s = await CreateScenarioAsync();
        SeriesCreatedDto created;
        using (ActAs(s.Owner.Id))
        {
            created = await _bookings.CreateSeriesAsync(Daily(s.SpaceId, 2, 2, Colleague(s.Rana)));
            (await RejectionCodeAsync(() => _bookings.RespondToSeriesAsync(created.SeriesId, Answer(InviteeResponseStatus.Accepted))))
                .ShouldBe(DixelsDomainErrorCodes.BookingOrganiserCannotRespond);
        }

        using (ActAs(s.Omar.Id))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _bookings.RespondToSeriesAsync(created.SeriesId, Answer(InviteeResponseStatus.Accepted)));
        }
    }

    // ---- Colleague search ----

    [Fact]
    public async Task The_colleague_search_finds_active_people_in_my_building_but_not_me()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);

        var found = await _colleagues.GetListAsync(new GetColleaguesInput { Filter = "test" });

        found.Items.Select(c => (c.Id, c.Name, c.Email)).ShouldBe(new[]
        {
            (s.Omar.Id, "Omar Test", s.Omar.Email),
            (s.Rana.Id, "Rana Test", s.Rana.Email),
        });
    }

    [Fact]
    public async Task The_colleague_search_needs_two_characters_and_returns_at_most_twenty()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.Owner.Id);

        (await _colleagues.GetListAsync(new GetColleaguesInput { Filter = " r " })).Items.ShouldBeEmpty();
        (await _colleagues.GetListAsync(new GetColleaguesInput { Filter = "ra" })).Items.ShouldHaveSingleItem().Id.ShouldBe(s.Rana.Id);
        (await _colleagues.GetListAsync(new GetColleaguesInput { Filter = "test", MaxResultCount = 1 })).Items.Count.ShouldBe(1);
        (await _colleagues.GetListAsync(new GetColleaguesInput { Filter = "test", MaxResultCount = 500 })).Items.Count.ShouldBe(2);
    }
}
