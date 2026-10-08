using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Emailing;
using Dixels.Emails.Rsvp;
using Dixels.Settings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// Guests answering in their own mail app (E6): the invite as a real meeting request, and their
/// replies read from the rsvp@ mailbox (a fake one here) and saved (see RsvpMailboxReader).
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class RsvpMailboxTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookings;
    private readonly FakeEmailSender _emails;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    public RsvpMailboxTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _emails = GetRequiredService<FakeEmailSender>();
    }

    /// <summary>The rsvp@ mailbox, in memory: what's in it and what was marked handled.</summary>
    private sealed class FakeMailbox : IRsvpMailbox
    {
        private int _nextId;
        public List<RsvpMailMessage> Unhandled { get; } = new();
        public List<string> Handled { get; } = new();

        public string Receive(string? calendar, string? method = null)
        {
            var id = (++_nextId).ToString();
            Unhandled.Add(new RsvpMailMessage(id, calendar, method));
            return id;
        }

        public Task<IReadOnlyList<RsvpMailMessage>> GetUnhandledAsync(int max, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RsvpMailMessage>>(Unhandled.Take(max).ToList());

        public Task MarkHandledAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
        {
            Handled.AddRange(ids);
            Unhandled.RemoveAll(m => ids.Contains(m.Id));
            return Task.CompletedTask;
        }

        public Task WaitForNewAsync(TimeSpan pollInterval, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed record Person(Guid Id, string Email);

    /// <summary>A UTC, 24/7 building with an 8-seat room; Dana books, Rana works there too.</summary>
    private sealed record Scenario(Guid SpaceId, Person Dana, Person Rana);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = new Building(
            Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0);
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

        return new Scenario(space.Id, await NewUserAsync("Dana"), await NewUserAsync("Rana"));
    });

    private IDisposable ActAs(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
        new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private async Task<BookingDto> BookAsync(Scenario s, DateTime? localStart = null)
    {
        var start = localStart ?? Tomorrow.AddHours(10);
        using (ActAs(s.Dana.Id))
        {
            return await _bookings.CreateAsync(new CreateBookingDto
            {
                SpaceId = s.SpaceId,
                LocalStart = start,
                LocalEnd = start.AddHours(1),
                Title = "Planning",
                IdempotencyKey = Guid.NewGuid().ToString(),
                Invitees = new List<InviteeDto> { new() { UserId = s.Rana.Id } },
            });
        }
    }

    /// <summary>Weekly at 09:00 for three weeks from tomorrow, every date booked.</summary>
    private async Task<SeriesCreatedDto> BookSeriesAsync(Scenario s)
    {
        using (ActAs(s.Dana.Id))
        {
            return await _bookings.CreateSeriesAsync(new CreateSeriesDto
            {
                SpaceId = s.SpaceId,
                LocalStart = Tomorrow.AddHours(9),
                LocalEnd = Tomorrow.AddHours(9).AddMinutes(30),
                Title = "Stand-up",
                Recurrence = new RecurrenceDto
                {
                    Frequency = RecurrenceFrequency.Weekly,
                    Interval = 1,
                    Weekdays = new[] { (int)Tomorrow.DayOfWeek },
                    EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(14)),
                },
                SkipDates = new List<DateOnly>(),
                IdempotencyKey = Guid.NewGuid().ToString(),
                Invitees = new List<InviteeDto> { new() { UserId = s.Rana.Id } },
            });
        }
    }

    /// <summary>A mail app's reply, as Outlook writes it; the attendee's address is deliberately not the guest's.</summary>
    private static string Reply(string uid, string partstat, DateTime? stampUtc = null, string? recurrenceId = null) => $"""
        BEGIN:VCALENDAR
        METHOD:REPLY
        PRODID:Microsoft Exchange Server 2010
        VERSION:2.0
        BEGIN:VEVENT
        ATTENDEE;PARTSTAT={partstat};CN=Someone:mailto:someone@elsewhere.io
        UID:{uid}{(recurrenceId is null ? "" : "\nRECURRENCE-ID:" + recurrenceId)}
        DTSTAMP:{(stampUtc ?? DateTime.UtcNow):yyyyMMdd'T'HHmmss'Z'}
        SEQUENCE:0
        END:VEVENT
        END:VCALENDAR
        """;

    private Task<List<BookingAttendee>> GuestRowsAsync(params Guid[] bookingIds) => WithUnitOfWorkAsync(async () =>
    {
        var db = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
        return await db.Set<BookingAttendee>().AsNoTracking().Where(a => bookingIds.Contains(a.BookingId)).ToListAsync();
    });

    private Task<BookingSeriesAttendee> SeriesRowAsync(Guid seriesId) => WithUnitOfWorkAsync(async () =>
    {
        var db = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
        return await db.Set<BookingSeriesAttendee>().AsNoTracking().SingleAsync(a => a.SeriesId == seriesId);
    });

    private async Task<int> ReadAsync(FakeMailbox mailbox) =>
        await GetRequiredService<RsvpMailboxReader>().HandleWaitingAsync(mailbox, CancellationToken.None);

    private Task SetRsvpMailboxAsync(bool enabled) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ISettingManager>().SetGlobalAsync(DixelsSettings.RsvpMailboxEnabled, enabled ? "true" : "false"));

    private static string Ics(SentEmail email)
    {
        var attachment = email.Mail.ShouldNotBeNull().Attachments.ShouldHaveSingleItem();
        attachment.ContentStream.Position = 0;
        return new StreamReader(attachment.ContentStream).ReadToEnd().Replace("\r\n ", "");
    }

    // ---- The invite ----

    [Fact]
    public async Task With_the_mailbox_on_an_invite_is_a_meeting_request_the_mail_app_answers()
    {
        var s = await CreateScenarioAsync();
        await SetRsvpMailboxAsync(true);
        try
        {
            _emails.Clear();
            await BookAsync(s);
            await QueuedJobs.RunAllAsync(ServiceProvider);

            var invite = _emails.Sent.Single(e => e.To == s.Rana.Email);
            invite.Mail!.AlternateViews.ShouldHaveSingleItem().ContentType.Parameters["method"].ShouldBe("REQUEST");
            var ics = Ics(invite);
            ics.ShouldContain("METHOD:REQUEST");
            ics.ShouldContain("RSVP=TRUE");
            ics.ShouldContain("mailto:rsvp@dixels.local");

            // The email points at the mail app's own buttons; the answer links shrink to one
            // backup line to the answer page, with no answer picked.
            invite.Body.ShouldContain("Answer with Accept or Decline at the top of this email.");
            invite.Body.ShouldContain("Answer here");
            invite.Body.ShouldMatch("href=\"[^\"]*/rsvp/[^\"?]+\"");
            invite.Body.ShouldNotContain("?answer=accepted");

            // The booker's own copy never asks anything.
            Ics(_emails.Sent.Single(e => e.To == s.Dana.Email)).ShouldContain("METHOD:PUBLISH");
        }
        finally
        {
            await SetRsvpMailboxAsync(false);
        }
    }

    [Fact]
    public async Task With_the_mailbox_off_an_invite_stays_add_to_calendar()
    {
        var s = await CreateScenarioAsync();
        _emails.Clear();

        await BookAsync(s);
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var invite = _emails.Sent.Single(e => e.To == s.Rana.Email);
        var ics = Ics(invite);
        ics.ShouldContain("METHOD:PUBLISH");
        ics.ShouldNotContain("RSVP=TRUE");

        // E7's answer links are the way to answer, as they were.
        invite.Body.ShouldContain("Will you come?");
        invite.Body.ShouldContain("?answer=accepted");
        invite.Body.ShouldContain("?answer=declined");
        invite.Body.ShouldNotContain("at the top of this email");
        invite.Body.ShouldNotContain("Answer here");
    }

    // ---- Reading the answers ----

    [Fact]
    public async Task An_accept_is_saved_by_the_guests_uid_alone_and_the_mail_marked_handled()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);
        var row = (await GuestRowsAsync(booking.Id)).Single();
        await QueuedJobs.RunAllAsync(ServiceProvider);
        _emails.Clear();

        var mailbox = new FakeMailbox();
        var id = mailbox.Receive(Reply(row.IcsUid, "ACCEPTED"));
        (await ReadAsync(mailbox)).ShouldBe(1);

        (await GuestRowsAsync(booking.Id)).Single().ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
        mailbox.Handled.ShouldBe(new[] { id });
        mailbox.Unhandled.ShouldBeEmpty();

        // An accept tells nobody, and E6 never emails of its own.
        await QueuedJobs.RunAllAsync(ServiceProvider);
        _emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_guest_who_changed_their_mind_while_the_mail_waited_gets_their_last_answer()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);
        var row = (await GuestRowsAsync(booking.Id)).Single();
        await QueuedJobs.RunAllAsync(ServiceProvider);
        _emails.Clear();

        // Read together (the reader was down), in whatever order the server lists them.
        var mailbox = new FakeMailbox();
        mailbox.Receive(Reply(row.IcsUid, "DECLINED", DateTime.UtcNow.AddMinutes(-1)));
        mailbox.Receive(Reply(row.IcsUid, "ACCEPTED", DateTime.UtcNow.AddMinutes(-2)));
        (await ReadAsync(mailbox)).ShouldBe(1);

        (await GuestRowsAsync(booking.Id)).Single().ResponseStatus.ShouldBe(InviteeResponseStatus.Declined);
        mailbox.Handled.Count.ShouldBe(2);

        // The booker hears of the decline once, from the same place as an answer in the app (E7).
        await QueuedJobs.RunAllAsync(ServiceProvider);
        _emails.Sent.ShouldHaveSingleItem().To.ShouldBe(s.Dana.Email);

        // A later reply, read later, changes it again.
        mailbox.Receive(Reply(row.IcsUid, "ACCEPTED"));
        (await ReadAsync(mailbox)).ShouldBe(1);
        (await GuestRowsAsync(booking.Id)).Single().ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
    }

    [Fact]
    public async Task Junk_maybe_and_unknown_guests_change_nothing_but_are_marked_handled()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);
        var row = (await GuestRowsAsync(booking.Id)).Single();

        var mailbox = new FakeMailbox();
        mailbox.Receive(null);                                              // a mail with no calendar
        mailbox.Receive("not a calendar");
        mailbox.Receive(Reply(row.IcsUid, "TENTATIVE"));                    // "Maybe" is no answer
        mailbox.Receive(Reply("nobody-has-this@dixels", "ACCEPTED"));       // taken off the list, or made up
        (await ReadAsync(mailbox)).ShouldBe(0);

        (await GuestRowsAsync(booking.Id)).Single().ResponseStatus.ShouldBe(InviteeResponseStatus.Pending);
        mailbox.Handled.Count.ShouldBe(4);
        mailbox.Unhandled.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_reply_older_than_the_guests_last_answer_is_skipped()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);
        var row = (await GuestRowsAsync(booking.Id)).Single();

        // Rana accepts in the app; a decline she sent from Outlook an hour ago is read only now.
        using (ActAs(s.Rana.Id))
        {
            await _bookings.RespondAsync(booking.Id, new RespondToInviteDto { Status = InviteeResponseStatus.Accepted });
        }

        var mailbox = new FakeMailbox();
        mailbox.Receive(Reply(row.IcsUid, "DECLINED", DateTime.UtcNow.AddHours(-1)));
        (await ReadAsync(mailbox)).ShouldBe(0);

        (await GuestRowsAsync(booking.Id)).Single().ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
        mailbox.Handled.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_cancelled_meeting_takes_no_answers()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);
        var row = (await GuestRowsAsync(booking.Id)).Single();
        using (ActAs(s.Dana.Id))
        {
            await _bookings.CancelAsync(booking.Id, new CancelBookingDto());
        }

        var mailbox = new FakeMailbox();
        mailbox.Receive(Reply(row.IcsUid, "ACCEPTED"));
        (await ReadAsync(mailbox)).ShouldBe(0);

        mailbox.Handled.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_series_reply_answers_every_upcoming_date_and_a_dated_one_just_that_date()
    {
        var s = await CreateScenarioAsync();
        var series = await BookSeriesAsync(s);
        var dates = series.Bookings.OrderBy(b => b.StartsAt).Select(b => b.Id).ToArray();
        dates.Length.ShouldBe(3);
        var seriesRow = await SeriesRowAsync(series.SeriesId);

        // Accept the whole series...
        var mailbox = new FakeMailbox();
        mailbox.Receive(Reply(seriesRow.IcsUid, "ACCEPTED", DateTime.UtcNow.AddMinutes(-1)));
        (await ReadAsync(mailbox)).ShouldBe(1);
        (await SeriesRowAsync(series.SeriesId)).ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
        (await GuestRowsAsync(dates)).ShouldAllBe(r => r.ResponseStatus == InviteeResponseStatus.Accepted);

        // ...then decline the second date only (its RECURRENCE-ID: the original 09:00 start, building time).
        var second = Tomorrow.AddDays(7).AddHours(9);
        mailbox.Receive(Reply(seriesRow.IcsUid, "DECLINED", recurrenceId: $"{second:yyyyMMdd'T'HHmmss}"));
        (await ReadAsync(mailbox)).ShouldBe(1);

        var rows = (await GuestRowsAsync(dates)).ToDictionary(r => r.BookingId);
        rows[dates[0]].ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
        rows[dates[1]].ResponseStatus.ShouldBe(InviteeResponseStatus.Declined);
        rows[dates[2]].ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
        (await SeriesRowAsync(series.SeriesId)).ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);

        // A date that isn't one of the series' is dropped.
        mailbox.Receive(Reply(seriesRow.IcsUid, "DECLINED", recurrenceId: $"{Tomorrow.AddDays(3).AddHours(9):yyyyMMdd'T'HHmmss}"));
        (await ReadAsync(mailbox)).ShouldBe(0);
        mailbox.Unhandled.ShouldBeEmpty();
    }
}
