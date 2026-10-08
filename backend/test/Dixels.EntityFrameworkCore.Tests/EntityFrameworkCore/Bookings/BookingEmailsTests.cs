using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Emailing;
using Dixels.Emails;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>The emails an employee gets about their own bookings (see BookingEmails).</summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingEmailsTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookingsAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;
    private readonly IdentityUserManager _userManager;
    private readonly UserLanguageManager _userLanguage;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly FakeEmailSender _emails;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    public BookingEmailsTests()
    {
        _bookingsAppService = GetRequiredService<IBookingsAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
        _spaceTypeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _userLanguage = GetRequiredService<UserLanguageManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _emails = GetRequiredService<FakeEmailSender>();
    }

    private sealed record Scenario(Guid UserId, string Email, Space Space);

    /// <summary>A UTC, 24/7 building with one 8-seat room, and an employee Dana Haddad in it.</summary>
    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await _buildingRepository.InsertAsync(new Building(
            Guid.NewGuid(), "en", "Riverside " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await _floorRepository.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var space = await _spaceRepository.InsertAsync(new Space(
            Guid.NewGuid(), floor.Id, "en", "Room 1", (await _spaceTypeRepository.FirstAsync()).Id, capacity: 8));

        var email = $"{Guid.NewGuid():N}@test.io";
        var user = new IdentityUser(Guid.NewGuid(), "emp" + Guid.NewGuid().ToString("N")[..8], email) { Name = "Dana", Surname = "Haddad" };
        user.SetBuildingId(building.Id);
        (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, email, space);
    });

    private IDisposable ActAs(Guid userId)
    {
        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
        })));
    }

    private static CreateBookingDto Request(Guid spaceId, string title = "Planning", string? key = null) => new()
    {
        SpaceId = spaceId,
        LocalStart = Tomorrow.AddHours(10),
        LocalEnd = Tomorrow.AddHours(11),
        Attendees = 2,
        Title = title,
        IdempotencyKey = key ?? Guid.NewGuid().ToString(),
    };

    /// <summary>Sends what's queued (a confirmation goes through a job), then forgets everything sent so far.</summary>
    private async Task ClearSentAsync()
    {
        await QueuedJobs.RunAllAsync(ServiceProvider);
        _emails.Clear();
    }

    /// <summary>What was sent to them, once the queued email jobs have run.</summary>
    private async Task<SentEmail[]> EmailsToAsync(Scenario s)
    {
        await QueuedJobs.RunAllAsync(ServiceProvider);
        return _emails.Sent.Where(e => e.To == s.Email).ToArray();
    }

    [Fact]
    public async Task A_new_booking_emails_its_details_to_the_employee()
    {
        var s = await CreateScenarioAsync();
        BookingDto created;
        using (ActAs(s.UserId))
        {
            created = await _bookingsAppService.CreateAsync(Request(s.Space.Id));
        }

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("Booking confirmed: Room 1, ");
        email.Body.ShouldContain("DIXELS");
        email.Body.ShouldContain("✓ Confirmed");
        email.Body.ShouldContain("Hi Dana Haddad,");
        email.Body.ShouldContain("You&#39;re booked: Planning");
        email.Body.ShouldContain("Room 1");
        email.Body.ShouldContain("Level 1");
        email.Body.ShouldContain("<span dir=\"ltr\">10:00–11:00</span> · <span dir=\"ltr\">UTC</span>");
        email.Body.ShouldContain("dir=\"ltr\"");
        // "View booking" opens this booking on its day in My calendar.
        email.Body.ShouldContain($"http://localhost:5173/my-calendar?view=day&amp;date={Tomorrow:yyyy-MM-dd}&amp;booking={created.Id}");
        email.Body.ShouldContain("http://localhost:5173/find-space");
        email.Body.ShouldNotContain(">Address<"); // the building has none
    }

    [Fact]
    public async Task A_retried_request_is_not_emailed_twice()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            var request = Request(s.Space.Id);
            await _bookingsAppService.CreateAsync(request);
            await _bookingsAppService.CreateAsync(request);
        }

        (await EmailsToAsync(s)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_rejected_booking_sends_nothing()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            var tooLong = Request(s.Space.Id);
            tooLong.LocalEnd = tooLong.LocalStart.AddHours(5);
            await Should.ThrowAsync<Exception>(() => _bookingsAppService.CreateAsync(tooLong));
        }

        (await EmailsToAsync(s)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_email_is_in_the_employees_language()
    {
        var s = await CreateScenarioAsync();
        await WithUnitOfWorkAsync(() => _userLanguage.SetAsync(s.UserId, "ar"));
        using (ActAs(s.UserId))
        {
            await _bookingsAppService.CreateAsync(Request(s.Space.Id));
        }

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("تم تأكيد الحجز: Room 1، ");
        email.Body.ShouldContain("dir=\"rtl\"");
        email.Body.ShouldContain("مرحبًا Dana Haddad،");
    }

    [Fact]
    public async Task What_the_employee_typed_is_shown_as_text_not_html()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            await _bookingsAppService.CreateAsync(Request(s.Space.Id, title: "<b>Board</b> & co"));
        }

        var body = (await EmailsToAsync(s)).ShouldHaveSingleItem().Body;
        body.ShouldContain("&lt;b&gt;Board&lt;/b&gt; &amp; co");
        body.ShouldNotContain("<b>Board</b>");
    }

    /// <summary>Daily 10:00–11:00 for three days from tomorrow.</summary>
    private static CreateSeriesDto ThreeDays(Guid spaceId) => new()
    {
        SpaceId = spaceId,
        LocalStart = Tomorrow.AddHours(10),
        LocalEnd = Tomorrow.AddHours(11),
        Attendees = 2,
        Title = "Stand-up",
        Recurrence = new RecurrenceDto
        {
            Frequency = RecurrenceFrequency.Daily,
            Interval = 1,
            EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(2)),
        },
        IdempotencyKey = Guid.NewGuid().ToString(),
    };

    [Fact]
    public async Task A_new_series_sends_one_email_for_all_its_dates()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            await _bookingsAppService.CreateSeriesAsync(ThreeDays(s.Space.Id));
        }

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldBe("Recurring booking confirmed: Room 1");
        email.Body.ShouldContain("You&#39;re booked: Stand-up");
        email.Body.ShouldContain($"Every day until {BookingFormat.Date(DateOnly.FromDateTime(Tomorrow.AddDays(2)))}");
        email.Body.ShouldContain($"From {BookingFormat.Date(DateOnly.FromDateTime(Tomorrow))}");
        email.Body.ShouldContain(">3<");
        email.Body.ShouldNotContain(">Not on<"); // every date was booked
    }

    [Fact]
    public async Task A_series_says_how_it_repeats_and_which_dates_it_skips()
    {
        var s = await CreateScenarioAsync();
        var request = ThreeDays(s.Space.Id);
        request.Recurrence = new RecurrenceDto
        {
            Frequency = RecurrenceFrequency.Weekly,
            Interval = 1,
            Weekdays = new[] { (int)Tomorrow.DayOfWeek },
            EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(14)),
        };
        request.SkipDates = new List<DateOnly> { DateOnly.FromDateTime(Tomorrow.AddDays(7)) };
        using (ActAs(s.UserId))
        {
            await _bookingsAppService.CreateSeriesAsync(request);
        }

        var body = (await EmailsToAsync(s)).ShouldHaveSingleItem().Body;
        body.ShouldContain($"Every {Tomorrow.DayOfWeek} until {BookingFormat.Date(DateOnly.FromDateTime(Tomorrow.AddDays(14)))}");
        body.ShouldContain(">Not on<");
        body.ShouldContain(BookingFormat.Date(DateOnly.FromDateTime(Tomorrow.AddDays(7))));
        body.ShouldContain(">2<"); // dates booked
    }

    /// <summary>Moves the scenario's building to Amman and gives it an English address.</summary>
    private Task MoveToAmmanAsync(Scenario s) => WithUnitOfWorkAsync(async () =>
    {
        var floor = await _floorRepository.GetAsync(s.Space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId, includeDetails: true);
        building.SetTimezone("Asia/Amman");
        building.SetAddresses(new Dictionary<string, string?> { ["en"] = "12 King Hussein St, Amman" });
        await _buildingRepository.UpdateAsync(building);
    });

    [Fact]
    public async Task Times_carry_the_buildings_zone_and_the_address_shows_when_set()
    {
        var s = await CreateScenarioAsync();
        await MoveToAmmanAsync(s);
        using (ActAs(s.UserId))
        {
            await _bookingsAppService.CreateAsync(Request(s.Space.Id));
        }

        var body = (await EmailsToAsync(s)).ShouldHaveSingleItem().Body;
        body.ShouldContain("<span dir=\"ltr\">10:00–11:00</span> · <span dir=\"ltr\">Amman time</span>");
        body.ShouldContain(">Address<");
        body.ShouldContain("12 King Hussein St, Amman");
    }

    [Fact]
    public async Task In_arabic_the_zone_is_its_gmt_offset_at_that_time()
    {
        var s = await CreateScenarioAsync();
        await MoveToAmmanAsync(s);
        await WithUnitOfWorkAsync(() => _userLanguage.SetAsync(s.UserId, "ar"));
        using (ActAs(s.UserId))
        {
            await _bookingsAppService.CreateAsync(Request(s.Space.Id));
        }

        // Jordan keeps UTC+3 all year.
        (await EmailsToAsync(s)).ShouldHaveSingleItem().Body.ShouldContain("<span dir=\"ltr\">10:00–11:00</span> · <span dir=\"ltr\">GMT+3</span>");
    }

    // ---- Cancelling their own booking ----

    [Fact]
    public async Task Cancelling_a_booking_emails_what_was_cancelled_and_why()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            var booking = await _bookingsAppService.CreateAsync(Request(s.Space.Id));
            await ClearSentAsync();

            await _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto { Reason = "Meeting moved <online>" });
        }

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("Booking cancelled: Room 1, ");
        email.Body.ShouldContain("✕ Cancelled");
        email.Body.ShouldContain("You cancelled Planning");
        email.Body.ShouldContain("text-decoration:line-through");
        email.Body.ShouldContain("10:00–11:00");
        email.Body.ShouldContain("Meeting moved &lt;online&gt;");
    }

    [Fact]
    public async Task Cancelling_a_whole_series_sends_one_email()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            var created = await _bookingsAppService.CreateSeriesAsync(ThreeDays(s.Space.Id));
            await ClearSentAsync();

            await _bookingsAppService.CancelAsync(created.Bookings[0].Id, new CancelBookingDto { Scope = CancelScope.Series });
        }

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldBe("Recurring booking cancelled: Room 1");
        email.Body.ShouldContain(">3<");
    }

    [Fact]
    public async Task A_cancel_that_fails_sends_nothing()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            var booking = await _bookingsAppService.CreateAsync(Request(s.Space.Id));
            await _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto());
            await ClearSentAsync();

            // Already cancelled: nothing changes, so nothing to tell them.
            await Should.ThrowAsync<Exception>(() => _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto()));
        }

        (await EmailsToAsync(s)).ShouldBeEmpty();
    }

    // ---- Cancelled by an admin: one email per person per admin action ----

    /// <summary>A booking tomorrow, straight into the database.</summary>
    private Task<Booking> BookDirectAsync(Scenario s, int startHour, int minutes = 60) =>
        BookDirectAsync(s, TimeSpan.FromHours(startHour), minutes);

    private Task<Booking> BookDirectAsync(Scenario s, TimeSpan from, int minutes) => WithUnitOfWorkAsync(() =>
        GetRequiredService<IBookingRepository>().InsertAsync(new Booking(
            Guid.NewGuid(), s.Space.Id, s.UserId,
            new DateTimeOffset(Tomorrow.Add(from), TimeSpan.Zero),
            new DateTimeOffset(Tomorrow.Add(from).AddMinutes(minutes), TimeSpan.Zero),
            2, title: "Planning", resolvedConstraintsJson: "{}", idempotencyKey: Guid.NewGuid().ToString())));

    private Task CancelAsAdminAsync(Guid bookingId, string reason) =>
        GetRequiredService<BookingImpactChecker>().CancelUpcomingAsAdminAsync(new[] { bookingId }, Admin, reason);

    private static readonly Guid Admin = Guid.NewGuid();

    private async Task<BackgroundJobRecord[]> AdminCancelJobsFor(Scenario s) =>
        (await QueuedJobs.WaitingAsync(ServiceProvider))
        .Where(j => j.JobName == "Dixels.Emails.AdminCancelled" && j.JobArgs.Contains(s.UserId.ToString()))
        .ToArray();

    [Fact]
    public async Task An_admin_cancel_emails_the_owner_what_was_cancelled_and_why()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookDirectAsync(s, 10);
        await ClearSentAsync();

        await WithUnitOfWorkAsync(() => CancelAsAdminAsync(booking.Id, "Closed: Floor works <now>"));

        // Queued with the cancel, sent by the job.
        _emails.Sent.ShouldNotContain(e => e.To == s.Email);
        (await AdminCancelJobsFor(s)).ShouldHaveSingleItem();
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("Your booking was cancelled: Planning, ");
        email.Body.ShouldContain("Hi Dana Haddad,");
        email.Body.ShouldContain("✕ Cancelled");
        email.Body.ShouldContain("Your booking was cancelled");
        email.Body.ShouldContain("Level 1");
        email.Body.ShouldContain("10:00–11:00");
        email.Body.ShouldContain("Closed: Floor works &lt;now&gt;");
        email.Body.ShouldContain("http://localhost:5173/find-space");
    }

    [Fact]
    public async Task One_admin_action_in_several_rounds_and_reasons_is_one_email_per_person()
    {
        var s = await CreateScenarioAsync();
        var other = await CreateScenarioAsync();
        var early = await BookDirectAsync(s, 9);
        var late = await BookDirectAsync(s, 15);
        var theirs = await BookDirectAsync(other, 11);
        await ClearSentAsync();

        // Three rounds, two reasons, two people — as a rule change that breaks two rules does.
        await WithUnitOfWorkAsync(async () =>
        {
            await CancelAsAdminAsync(late.Id, "Rules changed: Open 10:00–14:00 only");
            await CancelAsAdminAsync(early.Id, "Rules changed: Up to 30 minutes");
            await CancelAsAdminAsync(theirs.Id, "Rules changed: Up to 30 minutes");
        });
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldBe("2 of your bookings were cancelled by an admin");
        email.Body.ShouldContain("2 of your bookings were cancelled");
        // Soonest first, each with its own reason.
        email.Body.IndexOf("09:00–10:00", StringComparison.Ordinal).ShouldBeLessThan(email.Body.IndexOf("15:00–16:00", StringComparison.Ordinal));
        email.Body.ShouldContain("Rules changed: Open 10:00–14:00 only");
        email.Body.ShouldContain("Rules changed: Up to 30 minutes");
        (await EmailsToAsync(other)).ShouldHaveSingleItem().Subject.ShouldStartWith("Your booking was cancelled: Planning, ");
    }

    [Fact]
    public async Task An_admin_cancel_that_rolls_back_emails_nobody()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookDirectAsync(s, 10);
        await ClearSentAsync();

        // The emails are queued as the cancel saves, in its unit of work: one that fails
        // before then queues nothing.
        await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
        {
            await CancelAsAdminAsync(booking.Id, "Closed");
            throw new InvalidOperationException("Something after the cancel failed");
        }));

        (await AdminCancelJobsFor(s)).ShouldBeEmpty();
        await QueuedJobs.RunAllAsync(ServiceProvider);
        (await EmailsToAsync(s)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_admin_cancel_email_is_in_the_owners_language()
    {
        var s = await CreateScenarioAsync();
        await WithUnitOfWorkAsync(() => _userLanguage.SetAsync(s.UserId, "ar"));
        var first = await BookDirectAsync(s, 10);
        var second = await BookDirectAsync(s, 12);
        await ClearSentAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            await CancelAsAdminAsync(first.Id, "Closed");
            await CancelAsAdminAsync(second.Id, "Closed");
        });
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldBe("ألغى أحد المسؤولين 2 من حجوزاتك");
        email.Body.ShouldContain("dir=\"rtl\"");
        email.Body.ShouldContain("مرحبًا Dana Haddad،");
        email.Body.ShouldContain("أُلغي 2 من حجوزاتك");
        email.Body.ShouldContain("✕ ملغى");
        // The times and the zone (as its GMT offset in Arabic) stay left-to-right inside the Arabic text.
        email.Body.ShouldContain("<span dir=\"ltr\">10:00–11:00</span> · <span dir=\"ltr\">GMT</span>");
    }

    [Fact]
    public async Task A_long_list_shows_the_soonest_and_counts_the_rest()
    {
        var s = await CreateScenarioAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < AdminCancelEmailQueue.ShownBookings + 3; i++)
        {
            // Half-hour slots from midnight on.
            ids.Add((await BookDirectAsync(s, TimeSpan.FromMinutes(30 * i), 30)).Id);
        }
        await ClearSentAsync();

        await WithUnitOfWorkAsync(() => GetRequiredService<BookingImpactChecker>().CancelUpcomingAsAdminAsync(ids, Admin, "Closed"));
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldBe("23 of your bookings were cancelled by an admin");
        email.Body.Split(">When<").Length.ShouldBe(AdminCancelEmailQueue.ShownBookings + 1); // one block each
        email.Body.ShouldContain("…and 3 more. Open Dixels to see them all.");
        email.Body.ShouldContain("00:00–00:30");
        email.Body.ShouldNotContain("11:00–11:30"); // the 23rd
    }

    [Fact]
    public async Task A_removed_room_is_still_named_in_the_email()
    {
        var s = await CreateScenarioAsync();
        await BookDirectAsync(s, 10);
        await ClearSentAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            await _spaceRepository.DeleteAsync(s.Space.Id);
            await GetRequiredService<ILocalEventBus>().PublishAsync(new SpaceDeletedEvent(s.Space.Id, Admin));
        });
        await QueuedJobs.RunAllAsync(ServiceProvider); // cancels
        await QueuedJobs.RunAllAsync(ServiceProvider); // emails

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("Your booking was cancelled: Planning, ");
        email.Body.ShouldContain("The space was removed");
    }

    [Fact]
    public async Task A_switched_off_account_gets_no_admin_cancel_email()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookDirectAsync(s, 10);
        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(s.UserId);
            user.SetIsActive(false);
            (await _userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        });
        await ClearSentAsync();

        await WithUnitOfWorkAsync(() => CancelAsAdminAsync(booking.Id, "Account deactivated"));
        await QueuedJobs.RunAllAsync(ServiceProvider);

        (await EmailsToAsync(s)).ShouldBeEmpty();
    }

    // ---- Reminder before the start (default: 30 minutes) ----

    /// <summary>A booking starting 15–30 minutes from now (on the 15-minute grid), made by the employee.</summary>
    private async Task<BookingDto> BookSoonAsync(Scenario s)
    {
        var now = DateTime.UtcNow;
        var nextQuarter = new DateTime(now.Ticks - now.Ticks % TimeSpan.FromMinutes(15).Ticks, DateTimeKind.Utc).AddMinutes(15);
        var start = nextQuarter.AddMinutes(15);
        using (ActAs(s.UserId))
        {
            var request = Request(s.Space.Id);
            request.LocalStart = start;
            request.LocalEnd = start.AddHours(1);
            return await _bookingsAppService.CreateAsync(request);
        }
    }

    /// <summary>As if the booking had been made yesterday, before its reminder window.</summary>
    private Task MadeYesterdayAsync(Guid bookingId) => WithUnitOfWorkAsync(async () =>
    {
        var db = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
        await db.Bookings.Where(b => b.Id == bookingId)
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.CreationTime, DateTime.UtcNow.AddDays(-1)));
    });

    private Task<int> SendDueRemindersAsync() => GetRequiredService<BookingReminders>().SendDueAsync();

    [Fact]
    public async Task A_booking_starting_soon_gets_one_reminder()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookSoonAsync(s);
        await MadeYesterdayAsync(booking.Id);
        await ClearSentAsync();

        await SendDueRemindersAsync();
        await SendDueRemindersAsync();

        var email = (await EmailsToAsync(s)).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("Reminder: Room 1 at ");
        email.Body.ShouldContain("⏰ Reminder");
        email.Body.ShouldContain("Planning starts at ");
    }

    [Fact]
    public async Task A_booking_made_inside_the_window_gets_no_reminder()
    {
        var s = await CreateScenarioAsync();
        await BookSoonAsync(s);
        await ClearSentAsync();

        await SendDueRemindersAsync();

        (await EmailsToAsync(s)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_cancelled_booking_gets_no_reminder()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookSoonAsync(s);
        await MadeYesterdayAsync(booking.Id);
        using (ActAs(s.UserId))
        {
            await _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto());
        }
        await ClearSentAsync();

        await SendDueRemindersAsync();

        (await EmailsToAsync(s)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_booking_further_ahead_is_not_reminded_yet()
    {
        var s = await CreateScenarioAsync();
        BookingDto booking;
        using (ActAs(s.UserId))
        {
            booking = await _bookingsAppService.CreateAsync(Request(s.Space.Id)); // tomorrow
        }
        await MadeYesterdayAsync(booking.Id);
        await ClearSentAsync();

        await SendDueRemindersAsync();

        (await EmailsToAsync(s)).ShouldBeEmpty();
    }
}
