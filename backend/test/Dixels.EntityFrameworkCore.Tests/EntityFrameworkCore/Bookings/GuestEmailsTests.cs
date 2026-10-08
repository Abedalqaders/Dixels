using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Emailing;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>The emails a booking's guests get, and the guest parts of the booker's own (see BookingEmails.Guests).</summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class GuestEmailsTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookings;
    private readonly UserLanguageManager _userLanguage;
    private readonly FakeEmailSender _emails;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    public GuestEmailsTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _userLanguage = GetRequiredService<UserLanguageManager>();
        _emails = GetRequiredService<FakeEmailSender>();
    }

    private sealed record Person(Guid Id, string Email);

    /// <summary>A UTC, 24/7 building with an English-only address and an 8-seat room; Dana books, Rana and Omar work there too.</summary>
    private sealed record Scenario(Guid SpaceId, Guid BuildingId, Person Dana, Person Rana, Person Omar);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = new Building(
            Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0);
        building.SetName("ar", "المقر");
        building.SetAddresses(new Dictionary<string, string?> { ["en"] = "12 King Hussein St, Amman" });
        await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(building);
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, capacity: 8));

        async Task<Person> NewUserAsync(string name)
        {
            var key = Guid.NewGuid().ToString("N")[..8];
            var user = new IdentityUser(Guid.NewGuid(), "u" + key, $"{name.ToLowerInvariant()}.{key}@test.io") { Name = name, Surname = "Test" };
            user.SetBuildingId(building.Id);
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return new Person(user.Id, user.Email);
        }

        return new Scenario(space.Id, building.Id, await NewUserAsync("Dana"), await NewUserAsync("Rana"), await NewUserAsync("Omar"));
    });

    private IDisposable ActAs(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
        new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private static InviteeDto Colleague(Person p) => new() { UserId = p.Id };

    private static InviteeDto Outsider(string email, string? name = null) => new() { Email = email, Name = name };

    private static CreateBookingDto Request(Guid spaceId, params InviteeDto[] invitees) => new()
    {
        SpaceId = spaceId,
        LocalStart = Tomorrow.AddHours(10),
        LocalEnd = Tomorrow.AddHours(11),
        Attendees = 1 + invitees.Length,
        Title = "Planning",
        IdempotencyKey = Guid.NewGuid().ToString(),
        Invitees = invitees.ToList(),
    };

    private async Task<BookingDto> BookAsync(Scenario s, params InviteeDto[] invitees)
    {
        BookingDto created;
        using (ActAs(s.Dana.Id))
        {
            created = await _bookings.CreateAsync(Request(s.SpaceId, invitees));
        }

        await QueuedJobs.RunAllAsync(ServiceProvider);
        return created;
    }

    private SentEmail To(string email) => _emails.Sent.Where(e => e.To == email).ShouldHaveSingleItem();

    private static string Ics(SentEmail email)
    {
        // The HTML body, the calendar part beside it (what Outlook and Gmail read), and the same file attached.
        email.Mail.ShouldNotBeNull().AlternateViews.ShouldHaveSingleItem().ContentType.MediaType.ShouldBe("text/calendar");
        var attachment = email.Mail.Attachments.ShouldHaveSingleItem();
        attachment.Name.ShouldBe("invite.ics");
        attachment.ContentStream.Position = 0;
        // Unfolded: RFC 5545 wraps long lines at 75 characters.
        return new StreamReader(attachment.ContentStream).ReadToEnd().Replace("\r\n ", "");
    }

    private Task<List<string>> GuestUidsAsync(Guid bookingId) => WithUnitOfWorkAsync(async () =>
    {
        var db = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
        return await db.Set<BookingAttendee>().Where(a => a.BookingId == bookingId).OrderBy(a => a.Email).Select(a => a.IcsUid).ToListAsync();
    });

    [Fact]
    public async Task Every_guest_gets_their_own_invite_with_their_own_calendar_file()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        var booking = await BookAsync(s, Colleague(s.Rana), Outsider("guest@outside.io", "Sara Guest"));

        var rana = To(s.Rana.Email);
        rana.Subject.ShouldBe($"Invitation: Planning · {BookingFormat.Date(DateOnly.FromDateTime(Tomorrow))}, 10:00");
        rana.Body.ShouldContain("✉ Invitation");
        rana.Body.ShouldContain("Hi Rana Test,");
        rana.Body.ShouldContain("Dana Test invited you to Planning");
        rana.Body.ShouldContain("Sara Guest");                  // the other guest, by name
        rana.Body.ShouldNotContain("guest@outside.io");         // never their email
        rana.Body.ShouldContain("Open in Dixels");
        rana.Body.ShouldContain("Dixels sent you this email because Dana Test invited you to a meeting.");
        rana.Mail!.ReplyToList.ShouldHaveSingleItem().Address.ShouldBe(s.Dana.Email);
        rana.Mail.From!.DisplayName.ShouldBe("Dana Test (via Dixels)");

        var outsider = To("guest@outside.io");
        outsider.Body.ShouldContain("Hi Sara Guest,");
        outsider.Body.ShouldContain("Rana Test");
        outsider.Body.ShouldNotContain(s.Rana.Email);
        outsider.Body.ShouldNotContain("Open in Dixels");       // no account to open it in

        // Each file is that guest's own: their secret UID, only them as attendee, from the rsvp@ mailbox.
        var uids = await GuestUidsAsync(booking.Id);
        var ranaIcs = Ics(rana);
        var outsiderIcs = Ics(outsider);
        ranaIcs.ShouldContain("METHOD:PUBLISH");
        ranaIcs.ShouldContain("SEQUENCE:0");
        ranaIcs.ShouldContain("ORGANIZER;CN=Dana Test (via Dixels):mailto:rsvp@dixels.local");
        ranaIcs.ShouldContain($"mailto:{s.Rana.Email}");
        ranaIcs.ShouldNotContain("guest@outside.io");
        outsiderIcs.ShouldContain("mailto:guest@outside.io");
        new[] { ranaIcs, outsiderIcs }.Select(f => uids.Single(f.Contains)).Distinct().Count().ShouldBe(2);
        ranaIcs.ShouldContain("LOCATION:Room 1 · Level 1 · ");
        ranaIcs.ShouldContain("12 King Hussein St\\, Amman");
    }

    [Fact]
    public async Task The_bookers_confirmation_lists_the_guests_and_carries_their_own_calendar_file()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        var booking = await BookAsync(s, Colleague(s.Rana), Outsider("guest@outside.io"));

        var dana = To(s.Dana.Email);
        dana.Subject.ShouldStartWith("Booking confirmed: Room 1, ");
        dana.Body.ShouldContain(">Guests<");
        dana.Body.ShouldContain("Rana Test");
        dana.Body.ShouldContain("guest@outside.io (guest)"); // the booker typed it; no name given
        var ics = Ics(dana);
        ics.ShouldContain($"UID:{booking.Id}@dixels");
        ics.ShouldContain("METHOD:PUBLISH");
        ics.ShouldNotContain("ATTENDEE");
    }

    [Fact]
    public async Task Colleagues_get_their_own_language_and_outsiders_the_bookers()
    {
        var s = await CreateScenarioAsync();
        await WithUnitOfWorkAsync(() => _userLanguage.SetAsync(s.Rana.Id, "ar"));
        _emails.Clear();

        await BookAsync(s, Colleague(s.Rana), Colleague(s.Omar), Outsider("guest@outside.io"));

        var rana = To(s.Rana.Email);
        rana.Subject.ShouldStartWith("دعوة: Planning · ");
        rana.Body.ShouldContain("dir=\"rtl\"");
        rana.Body.ShouldContain("✉ دعوة");
        // The building has an English address only: better that than none.
        rana.Body.ShouldContain("12 King Hussein St, Amman");
        To(s.Omar.Email).Body.ShouldContain("dir=\"ltr\"");
        To("guest@outside.io").Body.ShouldContain("dir=\"ltr\""); // Dana's language
    }

    [Fact]
    public async Task A_booking_that_fails_invites_nobody()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            var tooLong = Request(s.SpaceId, Colleague(s.Rana));
            tooLong.LocalEnd = tooLong.LocalStart.AddHours(5);
            await Should.ThrowAsync<Exception>(() => _bookings.CreateAsync(tooLong));
        }

        await QueuedJobs.RunAllAsync(ServiceProvider);
        _emails.Sent.ShouldNotContain(e => e.To == s.Rana.Email);
    }

    [Fact]
    public async Task A_guest_taken_off_before_the_email_goes_is_not_invited()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        BookingDto booking;
        using (ActAs(s.Dana.Id))
        {
            booking = await _bookings.CreateAsync(Request(s.SpaceId, Colleague(s.Rana), Colleague(s.Omar)));
        }

        // Rana comes off the list before the queued emails are sent.
        await WithUnitOfWorkAsync(async () =>
        {
            var db = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
            await db.Set<BookingAttendee>().Where(a => a.BookingId == booking.Id && a.UserId == s.Rana.Id).ExecuteDeleteAsync();
        });
        await QueuedJobs.RunAllAsync(ServiceProvider);

        _emails.Sent.ShouldNotContain(e => e.To == s.Rana.Email);
        To(s.Omar.Email);
    }

    [Fact]
    public async Task A_booking_cancelled_before_the_invite_goes_sends_only_the_cancel()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            var booking = await _bookings.CreateAsync(Request(s.SpaceId, Colleague(s.Rana), Outsider("guest@outside.io")));
            // Called off before the queued invites are sent: there's nothing to come to.
            await _bookings.CancelAsync(booking.Id, new CancelBookingDto());
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        // No invite to something already off; just the cancel.
        To(s.Rana.Email).Subject.ShouldStartWith("Cancelled: Planning · ");
        To("guest@outside.io").Subject.ShouldStartWith("Cancelled: Planning · ");
    }

    [Fact]
    public async Task A_series_cancelled_before_the_invite_goes_sends_only_the_cancel()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            var created = await _bookings.CreateSeriesAsync(Series(s, Colleague(s.Rana)));
            await _bookings.CancelAsync(created.Bookings[0].Id, new CancelBookingDto { Scope = CancelScope.Series });
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        To(s.Rana.Email).Subject.ShouldBe("Cancelled: Stand-up");
    }

    /// <summary>Weekly at 09:00 for three weeks from tomorrow, the middle week skipped.</summary>
    private static CreateSeriesDto Series(Scenario s, params InviteeDto[] invitees) => new()
    {
        SpaceId = s.SpaceId,
        LocalStart = Tomorrow.AddHours(9),
        LocalEnd = Tomorrow.AddHours(9).AddMinutes(30),
        Attendees = 1 + invitees.Length,
        Title = "Stand-up",
        Recurrence = new RecurrenceDto
        {
            Frequency = RecurrenceFrequency.Weekly,
            Interval = 1,
            Weekdays = new[] { (int)Tomorrow.DayOfWeek },
            EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(14)),
        },
        SkipDates = new List<DateOnly> { DateOnly.FromDateTime(Tomorrow.AddDays(7)) },
        IdempotencyKey = Guid.NewGuid().ToString(),
        Invitees = invitees.ToList(),
    };

    [Fact]
    public async Task A_series_is_one_invite_with_its_repeat_rule_and_skipped_dates()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            await _bookings.CreateSeriesAsync(Series(s, Colleague(s.Rana)));
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var rana = To(s.Rana.Email);
        rana.Subject.ShouldBe($"Invitation: Stand-up · from {BookingFormat.Date(DateOnly.FromDateTime(Tomorrow))}");
        rana.Body.ShouldContain($"Every {Tomorrow.DayOfWeek} until");
        rana.Body.ShouldContain(">Not on<");
        var ics = Ics(rana);
        ics.ShouldContain("RRULE:FREQ=WEEKLY");
        ics.ShouldContain("EXDATE");
        ics.ShouldContain($"{Tomorrow.AddDays(7):yyyyMMdd}T090000");
    }

    // ---- E3: cancels ----

    /// <summary>The dates a CANCEL names, as their local start ("20261016T090000"), however the zone is written.</summary>
    private static string[] RecurrenceIds(string ics) =>
        ics.Split("\r\n").Where(l => l.StartsWith("RECURRENCE-ID", StringComparison.Ordinal))
            .Select(l => l[(l.IndexOf(':') + 1)..].TrimEnd('Z')).ToArray();

    /// <summary>The UID the guest's invite carried (their calendar file's).</summary>
    private static string UidOf(string ics) =>
        ics.Split("\r\n").Single(l => l.StartsWith("UID:", StringComparison.Ordinal))["UID:".Length..];

    /// <summary>Weekly at 09:00 for three weeks from tomorrow, every date booked.</summary>
    private static CreateSeriesDto ThreeWeeks(Scenario s, params InviteeDto[] invitees)
    {
        var series = Series(s, invitees);
        series.SkipDates = new List<DateOnly>();
        return series;
    }

    [Fact]
    public async Task An_owner_cancel_tells_every_guest_and_takes_the_invite_off_their_calendar()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, Colleague(s.Rana), Outsider("guest@outside.io", "Sara Guest"));
        var inviteUid = UidOf(Ics(To(s.Rana.Email)));
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            await _bookings.CancelAsync(booking.Id, new CancelBookingDto { Reason = "Moved online" });
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var rana = To(s.Rana.Email);
        rana.Subject.ShouldBe($"Cancelled: Planning · {BookingFormat.Date(DateOnly.FromDateTime(Tomorrow))}");
        rana.Body.ShouldContain("✕ Cancelled");
        rana.Body.ShouldContain("Planning is cancelled");
        rana.Body.ShouldContain("Dana Test cancelled this meeting. It's been taken off your calendar.");
        rana.Body.ShouldContain("text-decoration:line-through");
        rana.Mail!.ReplyToList.ShouldHaveSingleItem().Address.ShouldBe(s.Dana.Email);
        var ics = Ics(rana);
        ics.ShouldContain("METHOD:CANCEL");
        ics.ShouldContain("STATUS:CANCELLED");
        ics.ShouldContain("SEQUENCE:1");
        UidOf(ics).ShouldBe(inviteUid); // the same event, so it comes off
        rana.Mail.AlternateViews.ShouldHaveSingleItem().ContentType.Parameters["method"].ShouldBe("CANCEL");

        To("guest@outside.io").Body.ShouldContain("Hi Sara Guest,");
        To(s.Dana.Email).Body.ShouldContain("Your 2 guests were told it's cancelled.");
    }

    [Fact]
    public async Task One_date_of_a_series_is_cancelled_by_itself()
    {
        var s = await CreateScenarioAsync();
        SeriesCreatedDto created;
        using (ActAs(s.Dana.Id))
        {
            created = await _bookings.CreateSeriesAsync(ThreeWeeks(s, Colleague(s.Rana)));
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);
        var seriesUid = UidOf(Ics(To(s.Rana.Email)));
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            await _bookings.CancelAsync(created.Bookings[1].Id, new CancelBookingDto { Scope = CancelScope.This });
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var rana = To(s.Rana.Email);
        rana.Subject.ShouldBe($"Cancelled: Stand-up · {BookingFormat.Date(DateOnly.FromDateTime(Tomorrow.AddDays(7)))}");
        var ics = Ics(rana);
        UidOf(ics).ShouldBe(seriesUid);
        RecurrenceIds(ics).ShouldBe(new[] { $"{Tomorrow.AddDays(7):yyyyMMdd}T090000" });
        ics.ShouldNotContain("RRULE:FREQ=WEEKLY"); // the rest of the series stays
    }

    [Fact]
    public async Task This_and_following_is_one_email_naming_each_date()
    {
        var s = await CreateScenarioAsync();
        SeriesCreatedDto created;
        using (ActAs(s.Dana.Id))
        {
            created = await _bookings.CreateSeriesAsync(ThreeWeeks(s, Colleague(s.Rana)));
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            await _bookings.CancelAsync(created.Bookings[1].Id, new CancelBookingDto { Scope = CancelScope.ThisAndFollowing });
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var rana = To(s.Rana.Email);
        rana.Body.Split(">When<").Length.ShouldBe(3); // two dates, a block each
        var ics = Ics(rana);
        RecurrenceIds(ics).ShouldBe(new[] { $"{Tomorrow.AddDays(7):yyyyMMdd}T090000", $"{Tomorrow.AddDays(14):yyyyMMdd}T090000" });
    }

    [Fact]
    public async Task A_whole_series_cancel_takes_the_whole_event_off()
    {
        var s = await CreateScenarioAsync();
        SeriesCreatedDto created;
        using (ActAs(s.Dana.Id))
        {
            created = await _bookings.CreateSeriesAsync(ThreeWeeks(s, Colleague(s.Rana)));
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);
        var seriesUid = UidOf(Ics(To(s.Rana.Email)));
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            await _bookings.CancelAsync(created.Bookings[0].Id, new CancelBookingDto { Scope = CancelScope.Series });
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var rana = To(s.Rana.Email);
        rana.Subject.ShouldBe("Cancelled: Stand-up");
        rana.Body.ShouldContain($"Every {Tomorrow.DayOfWeek} until");
        var ics = Ics(rana);
        UidOf(ics).ShouldBe(seriesUid);
        ics.ShouldNotContain("RECURRENCE-ID");
    }

    [Fact]
    public async Task An_admin_cancel_is_one_email_per_guest_per_meeting_with_the_reason()
    {
        var s = await CreateScenarioAsync();
        var morning = await BookAsync(s, Colleague(s.Rana));
        BookingDto afternoon;
        using (ActAs(s.Dana.Id))
        {
            var request = Request(s.SpaceId, Colleague(s.Rana));
            request.LocalStart = Tomorrow.AddHours(14);
            request.LocalEnd = Tomorrow.AddHours(15);
            afternoon = await _bookings.CreateAsync(request);
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);
        _emails.Clear();

        // One admin action in two rounds, as a closure across many bookings is.
        await WithUnitOfWorkAsync(async () =>
        {
            var checker = GetRequiredService<BookingImpactChecker>();
            await checker.CancelUpcomingAsAdminAsync(new[] { morning.Id }, Guid.NewGuid(), "Closed: Floor works");
            await checker.CancelUpcomingAsAdminAsync(new[] { afternoon.Id }, Guid.NewGuid(), "Closed: Floor works");
        });
        await QueuedJobs.RunAllAsync(ServiceProvider);

        // A mail app acts on one CANCEL per message: a meeting each.
        var rana = _emails.Sent.Where(e => e.To == s.Rana.Email).ToList();
        rana.Count.ShouldBe(2);
        rana.ShouldAllBe(e => e.Body.Contains("An administrator cancelled this meeting."));
        rana.ShouldAllBe(e => e.Body.Contains("Closed: Floor works"));
        rana.Select(e => UidOf(Ics(e))).Distinct().Count().ShouldBe(2);
        // The booker's one email says their guest was told.
        To(s.Dana.Email).Body.ShouldContain("Your guest was told it's cancelled.");
    }

    [Fact]
    public async Task An_admin_cancel_that_rolls_back_tells_no_guest()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, Colleague(s.Rana));
        _emails.Clear();

        await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<BookingImpactChecker>().CancelUpcomingAsAdminAsync(new[] { booking.Id }, Guid.NewGuid(), "Closed");
            throw new InvalidOperationException("Something after the cancel failed");
        }));
        (await QueuedJobs.WaitingAsync(ServiceProvider)).ShouldNotContain(j => j.JobName == "Dixels.Emails.AdminCancelledGuest");
    }

    [Fact]
    public async Task A_switched_off_colleague_is_not_told_and_the_rest_are_in_their_language()
    {
        var s = await CreateScenarioAsync();
        await WithUnitOfWorkAsync(() => _userLanguage.SetAsync(s.Omar.Id, "ar"));
        var booking = await BookAsync(s, Colleague(s.Rana), Colleague(s.Omar));
        await WithUnitOfWorkAsync(async () =>
        {
            var users = GetRequiredService<IdentityUserManager>();
            var rana = await users.GetByIdAsync(s.Rana.Id);
            rana.SetIsActive(false);
            (await users.UpdateAsync(rana)).Succeeded.ShouldBeTrue();
        });
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            await _bookings.CancelAsync(booking.Id, new CancelBookingDto());
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        _emails.Sent.ShouldNotContain(e => e.To == s.Rana.Email);
        var omar = To(s.Omar.Email);
        omar.Subject.ShouldStartWith("أُلغي: Planning · ");
        omar.Body.ShouldContain("dir=\"rtl\"");
        omar.Body.ShouldContain("✕ ملغى");
    }

    // ---- E4: the guest list edited ----

    private Task EditGuestsAsync(Scenario s, Guid bookingId, params InviteeDto[] invitees) =>
        AsDanaAsync(() => _bookings.UpdateInviteesAsync(bookingId, new UpdateInviteesDto { Invitees = invitees.ToList(), Attendees = 1 + invitees.Length }), s);

    private Task EditSeriesGuestsAsync(Scenario s, Guid seriesId, params InviteeDto[] invitees) =>
        AsDanaAsync(() => _bookings.UpdateSeriesInviteesAsync(seriesId, new UpdateInviteesDto { Invitees = invitees.ToList(), Attendees = 1 + invitees.Length }), s);

    private async Task AsDanaAsync(Func<Task> act, Scenario s)
    {
        using (ActAs(s.Dana.Id))
        {
            await act();
        }

        await QueuedJobs.RunAllAsync(ServiceProvider);
    }

    [Fact]
    public async Task Editing_the_list_invites_who_was_added_and_cancels_for_who_was_removed()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, Colleague(s.Rana), Outsider("guest@outside.io", "Sara Guest"));
        var ranaInviteUid = UidOf(Ics(To(s.Rana.Email)));
        _emails.Clear();

        // Rana off, Omar on, the outsider stays.
        await EditGuestsAsync(s, booking.Id, Colleague(s.Omar), Outsider("guest@outside.io", "Sara Guest"));

        var omar = To(s.Omar.Email);
        omar.Subject.ShouldStartWith("Invitation: Planning · ");
        omar.Body.ShouldContain("Dana Test added you to Planning");
        omar.Body.ShouldContain("Sara Guest"); // the guest who stayed, by name
        var omarIcs = Ics(omar);
        omarIcs.ShouldContain("METHOD:PUBLISH");
        omarIcs.ShouldContain("SEQUENCE:0");
        UidOf(omarIcs).ShouldNotBe(ranaInviteUid);

        var rana = To(s.Rana.Email);
        rana.Subject.ShouldStartWith("Removed: Planning · ");
        rana.Body.ShouldContain("Dana Test removed you from Planning");
        rana.Body.ShouldContain("It's been taken off your calendar.");
        var ranaIcs = Ics(rana);
        ranaIcs.ShouldContain("METHOD:CANCEL");
        ranaIcs.ShouldContain("SEQUENCE:1");
        UidOf(ranaIcs).ShouldBe(ranaInviteUid);

        // Whoever stayed, and the booker, hear nothing.
        _emails.Sent.ShouldNotContain(e => e.To == "guest@outside.io" || e.To == s.Dana.Email);
    }

    [Fact]
    public async Task Someone_removed_and_added_back_gets_a_new_calendar_copy()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, Colleague(s.Rana));
        var first = UidOf(Ics(To(s.Rana.Email)));

        await EditGuestsAsync(s, booking.Id);
        await EditGuestsAsync(s, booking.Id, Colleague(s.Rana));

        var mails = _emails.Sent.Where(e => e.To == s.Rana.Email).ToList();
        mails.Select(e => e.Subject[..e.Subject.IndexOf(':')]).ShouldBe(new[] { "Invitation", "Removed", "Invitation" });
        UidOf(Ics(mails[1])).ShouldBe(first);
        UidOf(Ics(mails[2])).ShouldNotBe(first);
    }

    [Fact]
    public async Task On_a_series_the_added_get_a_series_invite_and_the_removed_a_whole_series_cancel()
    {
        var s = await CreateScenarioAsync();
        SeriesCreatedDto created;
        using (ActAs(s.Dana.Id))
        {
            created = await _bookings.CreateSeriesAsync(ThreeWeeks(s, Colleague(s.Rana)));
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);
        var ranaUid = UidOf(Ics(To(s.Rana.Email)));
        _emails.Clear();

        await EditSeriesGuestsAsync(s, created.SeriesId, Colleague(s.Omar));

        var omar = To(s.Omar.Email);
        omar.Body.ShouldContain("Dana Test added you to Stand-up");
        omar.Body.ShouldContain($"Every {Tomorrow.DayOfWeek} until");
        Ics(omar).ShouldContain("RRULE:FREQ=WEEKLY");

        var rana = To(s.Rana.Email);
        rana.Body.ShouldContain("Dana Test removed you from Stand-up");
        var ranaIcs = Ics(rana);
        UidOf(ranaIcs).ShouldBe(ranaUid);
        ranaIcs.ShouldContain("METHOD:CANCEL");
        ranaIcs.ShouldNotContain("RECURRENCE-ID"); // off the whole series
    }

    [Fact]
    public async Task A_guest_list_edit_that_fails_emails_nobody()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, Colleague(s.Rana));
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            // Too few attendees for the guests: refused, nothing changes.
            await Should.ThrowAsync<Exception>(() => _bookings.UpdateInviteesAsync(booking.Id,
                new UpdateInviteesDto { Invitees = new List<InviteeDto> { Colleague(s.Omar), Colleague(s.Rana) }, Attendees = 1 }));
        }
        await QueuedJobs.RunAllAsync(ServiceProvider);

        _emails.Sent.ShouldBeEmpty();
    }
}
