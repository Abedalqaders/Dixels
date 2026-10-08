using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Emailing;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// Answering by the links in the invite email (E7): each guest's own secret link, the public
/// answer page's calls (no sign-in), the same rules as in the app, and the booker's email when a
/// guest declines, however they answered.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class GuestAnswerLinksTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly IBookingsAppService _bookings;
    private readonly IRsvpAppService _rsvp;
    private readonly GuestLinks _guestLinks;
    private readonly IBookingRepository _bookingRepository;
    private readonly FakeEmailSender _emails;

    public GuestAnswerLinksTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _rsvp = GetRequiredService<IRsvpAppService>();
        _guestLinks = GetRequiredService<GuestLinks>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _emails = GetRequiredService<FakeEmailSender>();
    }

    private sealed record Person(Guid Id, string Email);

    /// <summary>A UTC, 24/7 building with an 8-seat room; Dana books, Rana works there too, Sam is from outside.</summary>
    private sealed record Scenario(Guid SpaceId, Person Dana, Person Rana, string Sam);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
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

        return new Scenario(space.Id, await NewUserAsync("Dana"), await NewUserAsync("Rana"), $"sam.{Guid.NewGuid():N}@outside.io");
    });

    private IDisposable ActAs(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
        new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private async Task<BookingDto> BookAsync(Scenario s)
    {
        BookingDto created;
        using (ActAs(s.Dana.Id))
        {
            created = await _bookings.CreateAsync(new CreateBookingDto
            {
                SpaceId = s.SpaceId,
                LocalStart = Tomorrow.AddHours(10),
                LocalEnd = Tomorrow.AddHours(11),
                Title = "Planning",
                IdempotencyKey = Guid.NewGuid().ToString(),
                Invitees = new() { new InviteeDto { UserId = s.Rana.Id }, new InviteeDto { Email = s.Sam, Name = "Sam Lee" } },
            });
        }

        await QueuedJobs.RunAllAsync(ServiceProvider);
        return created;
    }

    private async Task<SeriesCreatedDto> BookSeriesAsync(Scenario s)
    {
        SeriesCreatedDto created;
        using (ActAs(s.Dana.Id))
        {
            created = await _bookings.CreateSeriesAsync(new CreateSeriesDto
            {
                SpaceId = s.SpaceId,
                LocalStart = Tomorrow.AddHours(12),
                LocalEnd = Tomorrow.AddHours(13),
                Title = "Stand-up",
                Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(2)) },
                IdempotencyKey = Guid.NewGuid().ToString(),
                Invitees = new() { new InviteeDto { UserId = s.Rana.Id }, new InviteeDto { Email = s.Sam, Name = "Sam Lee" } },
            });
        }

        await QueuedJobs.RunAllAsync(ServiceProvider);
        return created;
    }

    /// <summary>The guest's link token, read from the Accept button in the invite they were sent.</summary>
    private string TokenFromInvite(string email)
    {
        var invite = _emails.Sent.Where(e => e.To == email).ShouldHaveSingleItem();
        var accept = Regex.Match(invite.Body, @"/rsvp/([A-Za-z0-9_\-\.]+)\?answer=accepted");
        accept.Success.ShouldBeTrue();
        invite.Body.ShouldContain($"/rsvp/{accept.Groups[1].Value}?answer=declined");
        return accept.Groups[1].Value;
    }

    private int DeclineEmailsTo(Person booker) =>
        _emails.Sent.Count(e => e.To == booker.Email && e.Subject.StartsWith("Declined:"));

    private Task<GuestInvitationDto> AnswerAsync(string token, InviteeResponseStatus answer) =>
        _rsvp.AnswerAsync(new GuestAnswerInput { Token = token, Answer = answer });

    [Fact]
    public async Task An_outside_guest_answers_from_the_invite_and_the_booker_hears_of_a_decline()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);
        var token = TokenFromInvite(s.Sam);

        // Opening the page changes nothing.
        var shown = await _rsvp.LookupAsync(new GuestLinkInput { Token = token });
        shown.ShouldSatisfyAllConditions(
            d => d.GuestName.ShouldBe("Sam Lee"),
            d => d.InvitedBy.ShouldBe("Dana Test"),
            d => d.Title.ShouldBe("Planning"),
            d => d.SpaceName.ShouldBe("Room 1"),
            d => d.LocalStart.ShouldBe(Tomorrow.AddHours(10)),
            d => d.Recurrence.ShouldBeNull(),
            d => d.MyResponse.ShouldBe(InviteeResponseStatus.Pending),
            d => d.IsOpen.ShouldBeTrue(),
            d => d.Language.ShouldBe("en"));

        (await AnswerAsync(token, InviteeResponseStatus.Accepted)).MyResponse.ShouldBe(InviteeResponseStatus.Accepted);
        (await AnswerAsync(token, InviteeResponseStatus.Declined)).MyResponse.ShouldBe(InviteeResponseStatus.Declined);
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(booking.Id));
        stored.Invitees.Single(i => i.Email is not null).ResponseStatus.ShouldBe(InviteeResponseStatus.Declined);
        stored.Invitees.Single(i => i.UserId == s.Rana.Id).ResponseStatus.ShouldBe(InviteeResponseStatus.Pending);

        var declined = _emails.Sent.Where(e => e.To == s.Dana.Email && e.Subject.StartsWith("Declined:")).ShouldHaveSingleItem();
        declined.Subject.ShouldContain("Sam Lee can't make it to Planning");
    }

    [Fact]
    public async Task A_colleague_can_answer_by_link_too()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);

        (await AnswerAsync(TokenFromInvite(s.Rana.Email), InviteeResponseStatus.Accepted)).GuestName.ShouldBe("Rana Test");

        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(booking.Id));
        stored.Invitees.Single(i => i.UserId == s.Rana.Id).ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);
    }

    [Fact]
    public async Task A_series_link_answers_every_upcoming_date_with_one_email_to_the_booker()
    {
        var s = await CreateScenarioAsync();
        var series = await BookSeriesAsync(s);
        var token = TokenFromInvite(s.Sam);
        token.ShouldStartWith("s.");

        var shown = await AnswerAsync(token, InviteeResponseStatus.Declined);
        shown.Recurrence.ShouldNotBeNull().Frequency.ShouldBe(RecurrenceFrequency.Daily);
        shown.LocalStart.ShouldBe(Tomorrow.AddHours(12));
        await QueuedJobs.RunAllAsync(ServiceProvider);

        var dates = await WithUnitOfWorkAsync(() => _bookingRepository.GetListAsync(b => b.SeriesId == series.SeriesId, includeDetails: true));
        dates.Count.ShouldBe(3);
        dates.ShouldAllBe(b => b.Invitees.Single(i => i.Email != null).ResponseStatus == InviteeResponseStatus.Declined);
        DeclineEmailsTo(s.Dana).ShouldBe(1);
    }

    [Fact]
    public async Task The_booker_is_told_only_when_the_answer_turns_to_declined_however_it_came()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);

        // In the app (T5): declining twice in a row is one change.
        using (ActAs(s.Rana.Id))
        {
            await _bookings.RespondAsync(booking.Id, new RespondToInviteDto { Status = InviteeResponseStatus.Declined });
            await _bookings.RespondAsync(booking.Id, new RespondToInviteDto { Status = InviteeResponseStatus.Declined });
            await _bookings.RespondAsync(booking.Id, new RespondToInviteDto { Status = InviteeResponseStatus.Accepted });
        }

        await QueuedJobs.RunAllAsync(ServiceProvider);
        DeclineEmailsTo(s.Dana).ShouldBe(1);

        // By link: accepted → declined is a change again.
        await AnswerAsync(TokenFromInvite(s.Rana.Email), InviteeResponseStatus.Declined);
        await QueuedJobs.RunAllAsync(ServiceProvider);
        DeclineEmailsTo(s.Dana).ShouldBe(2);
    }

    [Fact]
    public async Task Answers_close_once_the_meeting_is_cancelled_and_the_page_says_so()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);
        var token = TokenFromInvite(s.Sam);
        using (ActAs(s.Dana.Id))
        {
            await _bookings.CancelAsync(booking.Id, new CancelBookingDto());
        }

        (await _rsvp.LookupAsync(new GuestLinkInput { Token = token })).IsOpen.ShouldBeFalse();
        (await Should.ThrowAsync<BusinessException>(() => AnswerAsync(token, InviteeResponseStatus.Accepted)))
            .Code.ShouldBe(DixelsDomainErrorCodes.BookingResponseClosed);
    }

    [Fact]
    public async Task Answers_close_when_the_meeting_starts()
    {
        var s = await CreateScenarioAsync();
        // Under way: made directly, as the app can't book in the past.
        var token = await WithUnitOfWorkAsync(async () =>
        {
            var start = DateTimeOffset.UtcNow.AddMinutes(-5);
            var underWay = new Booking(Guid.NewGuid(), s.SpaceId, s.Dana.Id, start, start.AddHours(1), 2, "Now", "{}", Guid.NewGuid().ToString());
            underWay.SetInvitees(new[] { new Invitee(null, s.Sam, null) }, GetRequiredService<IGuidGenerator>());
            await _bookingRepository.InsertAsync(underWay);
            return _guestLinks.TokenFor(underWay.Invitees.Single());
        });

        (await _rsvp.LookupAsync(new GuestLinkInput { Token = token })).IsOpen.ShouldBeFalse();
        (await Should.ThrowAsync<BusinessException>(() => AnswerAsync(token, InviteeResponseStatus.Declined)))
            .Code.ShouldBe(DixelsDomainErrorCodes.BookingResponseClosed);
    }

    [Fact]
    public async Task A_removed_guest_or_a_forged_link_finds_nothing()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s);
        var token = TokenFromInvite(s.Sam);

        // Tampered: another row id, the wrong kind, a changed signature, garbage.
        var parts = token.Split('.');
        var forged = new[]
        {
            $"b.{System.Buffers.Text.Base64Url.EncodeToString(Guid.NewGuid().ToByteArray())}.{parts[2]}",
            $"s.{parts[1]}.{parts[2]}",
            $"b.{parts[1]}.{parts[2][..^2]}AA",
            "not-a-token",
        };
        foreach (var bad in forged)
        {
            (await Should.ThrowAsync<BusinessException>(() => _rsvp.LookupAsync(new GuestLinkInput { Token = bad })))
                .Code.ShouldBe(DixelsDomainErrorCodes.GuestLinkNotFound);
        }

        // Signed with another key: what a leaked database alone could try.
        var otherKey = new GuestLinks(_bookingRepository, GetRequiredService<IRepository<BookingSeries, Guid>>(),
            Options.Create(new GuestLinkOptions { Key = "someone-else's-key" }));
        (await WithUnitOfWorkAsync(() => otherKey.ResolveAsync(token))).ShouldBeNull();

        // Taken off the list: their row is gone, and the link with it.
        using (ActAs(s.Dana.Id))
        {
            await _bookings.UpdateInviteesAsync(booking.Id, new UpdateInviteesDto { Invitees = new() { new InviteeDto { UserId = s.Rana.Id } } });
        }

        (await Should.ThrowAsync<BusinessException>(() => AnswerAsync(token, InviteeResponseStatus.Accepted)))
            .Code.ShouldBe(DixelsDomainErrorCodes.GuestLinkNotFound);
    }
}
