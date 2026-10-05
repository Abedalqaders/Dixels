using System;
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

    private SentEmail[] EmailsTo(Scenario s) => _emails.Sent.Where(e => e.To == s.Email).ToArray();

    [Fact]
    public async Task A_new_booking_emails_its_details_to_the_employee()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            await _bookingsAppService.CreateAsync(Request(s.Space.Id));
        }

        var email = EmailsTo(s).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("Booking confirmed: Room 1, ");
        email.Body.ShouldContain("Hi Dana Haddad,");
        email.Body.ShouldContain("Room 1");
        email.Body.ShouldContain("Level 1");
        email.Body.ShouldContain("10:00–11:00");
        email.Body.ShouldContain("Planning");
        email.Body.ShouldContain("dir=\"ltr\"");
        email.Body.ShouldContain("http://localhost:5173/my-calendar");
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

        EmailsTo(s).ShouldHaveSingleItem();
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

        EmailsTo(s).ShouldBeEmpty();
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

        var email = EmailsTo(s).ShouldHaveSingleItem();
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

        var body = EmailsTo(s).ShouldHaveSingleItem().Body;
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

        var email = EmailsTo(s).ShouldHaveSingleItem();
        email.Subject.ShouldBe("Recurring booking confirmed: Room 1");
        email.Body.ShouldContain(">3<");
        email.Body.ShouldContain("Stand-up");
    }

    // ---- Cancelling their own booking ----

    [Fact]
    public async Task Cancelling_a_booking_emails_what_was_cancelled_and_why()
    {
        var s = await CreateScenarioAsync();
        using (ActAs(s.UserId))
        {
            var booking = await _bookingsAppService.CreateAsync(Request(s.Space.Id));
            _emails.Clear();

            await _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto { Reason = "Meeting moved <online>" });
        }

        var email = EmailsTo(s).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("Booking cancelled: Room 1, ");
        email.Body.ShouldContain("Your booking is cancelled.");
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
            _emails.Clear();

            await _bookingsAppService.CancelAsync(created.Bookings[0].Id, new CancelBookingDto { Scope = CancelScope.Series });
        }

        var email = EmailsTo(s).ShouldHaveSingleItem();
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
            _emails.Clear();

            // Already cancelled: nothing changes, so nothing to tell them.
            await Should.ThrowAsync<Exception>(() => _bookingsAppService.CancelAsync(booking.Id, new CancelBookingDto()));
        }

        EmailsTo(s).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_admin_cancel_is_announced_but_not_emailed()
    {
        var s = await CreateScenarioAsync();
        BookingDto booking;
        using (ActAs(s.UserId))
        {
            booking = await _bookingsAppService.CreateAsync(Request(s.Space.Id));
        }
        _emails.Clear();

        // Listening as any other listener would: the event is there for them, the email isn't.
        BookingsCancelledEvent? heard = null;
        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingsCancelledEvent>(e => { heard = e; return Task.CompletedTask; }))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var stored = await GetRequiredService<IBookingRepository>().GetAsync(booking.Id);
                await GetRequiredService<BookingImpactChecker>().CancelAsAdminAsync(new[] { stored }, Guid.NewGuid(), _ => "Room closed");
            });
        }

        heard.ShouldNotBeNull();
        heard.ByAdmin.ShouldBeTrue();
        heard.Bookings.ShouldHaveSingleItem().Id.ShouldBe(booking.Id);
        EmailsTo(s).ShouldBeEmpty();
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
        _emails.Clear();

        await SendDueRemindersAsync();
        await SendDueRemindersAsync();

        var email = EmailsTo(s).ShouldHaveSingleItem();
        email.Subject.ShouldStartWith("Reminder: Room 1 at ");
        email.Body.ShouldContain("Your booking starts soon.");
    }

    [Fact]
    public async Task A_booking_made_inside_the_window_gets_no_reminder()
    {
        var s = await CreateScenarioAsync();
        await BookSoonAsync(s);
        _emails.Clear();

        await SendDueRemindersAsync();

        EmailsTo(s).ShouldBeEmpty();
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
        _emails.Clear();

        await SendDueRemindersAsync();

        EmailsTo(s).ShouldBeEmpty();
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
        _emails.Clear();

        await SendDueRemindersAsync();

        EmailsTo(s).ShouldBeEmpty();
    }
}
