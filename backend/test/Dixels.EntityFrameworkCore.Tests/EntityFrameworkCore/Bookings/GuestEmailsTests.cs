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
    public async Task A_series_is_one_invite_with_its_repeat_rule_and_skipped_dates()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        using (ActAs(s.Dana.Id))
        {
            await _bookings.CreateSeriesAsync(new CreateSeriesDto
            {
                SpaceId = s.SpaceId,
                LocalStart = Tomorrow.AddHours(9),
                LocalEnd = Tomorrow.AddHours(9).AddMinutes(30),
                Attendees = 2,
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
                Invitees = new List<InviteeDto> { Colleague(s.Rana) },
            });
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
}
