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
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// "Busy then": a colleague with their own confirmed booking, or a meeting they accepted, at
/// the time is flagged in the guest picker — never refused, never told with what. And my own
/// accepted meetings at a time I book give me a heads-up, even where double bookings are blocked.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingBusyTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookings;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    public BookingBusyTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    /// <summary>A UTC, 24/7 building with two 8-seat rooms; the owner, three colleagues and a stranger work there.</summary>
    private sealed record Scenario(Guid BuildingId, Guid Room, Guid OtherRoom, Guid Owner, Guid Rana, Guid Omar, Guid Lina, Guid Stranger);

    private Task<Scenario> CreateScenarioAsync(OwnOverlapPolicy policy = OwnOverlapPolicy.Warn) => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0, ownOverlapPolicy: policy));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var spaces = GetRequiredService<IRepository<Space, Guid>>();
        var room = await spaces.InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, capacity: 8));
        var other = await spaces.InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 2", spaceType.Id, capacity: 8));

        async Task<Guid> NewUserAsync(string name)
        {
            var key = Guid.NewGuid().ToString("N")[..8];
            var user = new IdentityUser(Guid.NewGuid(), "u" + key, $"{name.ToLowerInvariant()}.{key}@test.io") { Name = name };
            user.SetBuildingId(building.Id);
            (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return user.Id;
        }

        return new Scenario(building.Id, room.Id, other.Id,
            await NewUserAsync("Owner"), await NewUserAsync("Rana"), await NewUserAsync("Omar"), await NewUserAsync("Lina"), await NewUserAsync("Stranger"));
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private static CreateBookingDto Booking(Guid room, int fromHour, int toHour, int attendees = 2, params Guid[] guests) => new()
    {
        SpaceId = room,
        LocalStart = Tomorrow.AddHours(fromHour),
        LocalEnd = Tomorrow.AddHours(toHour),
        IdempotencyKey = Guid.NewGuid().ToString(),
        Invitees = guests.Select(g => new InviteeDto { UserId = g }).ToList(),
    };

    private async Task<BookingDto> BookAsAsync(Guid who, CreateBookingDto request)
    {
        using (ActAs(who))
        {
            return await _bookings.CreateAsync(request);
        }
    }

    /// <summary>Sets a guest's answer directly (answering arrives in its own task).</summary>
    private Task AnswerAsync(Guid bookingId, Guid userId, InviteeResponseStatus status) => WithUnitOfWorkAsync(async () =>
    {
        var dbContext = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
        var row = await dbContext.Set<BookingAttendee>().SingleAsync(a => a.BookingId == bookingId && a.UserId == userId);
        dbContext.Entry(row).Property(nameof(BookingAttendee.ResponseStatus)).CurrentValue = status;
        await dbContext.SaveChangesAsync();
    });

    private static bool BusyIn(BookingPreviewDto preview, Guid userId) => preview.Invitees.Single(i => i.UserId == userId).IsBusy;

    // ---- The picker's "Busy then" ----

    [Fact]
    public async Task A_colleague_with_their_own_booking_or_an_accepted_meeting_then_is_busy()
    {
        var s = await CreateScenarioAsync();
        // Rana has her own room 10–11; Omar accepted Lina's meeting 10:30–11:30; Lina is free.
        await BookAsAsync(s.Rana, Booking(s.OtherRoom, 10, 11));
        var meeting = await BookAsAsync(s.Lina, new CreateBookingDto
        {
            SpaceId = s.OtherRoom, LocalStart = Tomorrow.AddHours(11), LocalEnd = Tomorrow.AddHours(12), 
            IdempotencyKey = Guid.NewGuid().ToString(), Invitees = [new InviteeDto { UserId = s.Omar }],
        });
        await AnswerAsync(meeting.Id, s.Omar, InviteeResponseStatus.Accepted);

        using var _ = ActAs(s.Owner);
        var preview = await _bookings.PreviewAsync(Booking(s.Room, 10, 12, 4, s.Rana, s.Omar, s.Lina));

        preview.IsValid.ShouldBeTrue();
        BusyIn(preview, s.Rana).ShouldBeTrue();
        BusyIn(preview, s.Omar).ShouldBeTrue();
        // Lina is the organiser of the 11–12 meeting: her own booking, so she's busy too.
        BusyIn(preview, s.Lina).ShouldBeTrue();
        preview.Invitees.Single(i => i.UserId == s.Rana).BusyDates.ShouldBe(1);

        // When, on the building's clock, cut to the booking's 10–12 — never with what.
        Times(preview, s.Rana).ShouldBe(new[] { (Tomorrow.AddHours(10), Tomorrow.AddHours(11)) });
        Times(preview, s.Omar).ShouldBe(new[] { (Tomorrow.AddHours(11), Tomorrow.AddHours(12)) });
        Times(preview, s.Lina).ShouldBe(new[] { (Tomorrow.AddHours(11), Tomorrow.AddHours(12)) });
    }

    private static (DateTime, DateTime)[] Times(BookingPreviewDto preview, Guid userId) =>
        preview.Invitees.Single(i => i.UserId == userId).BusyTimes.Select(t => (t.LocalStart, t.LocalEnd)).ToArray();

    [Fact]
    public async Task Busy_times_are_cut_to_the_slot_and_overlapping_ones_merged()
    {
        var s = await CreateScenarioAsync();
        // Rana: her own room 08–10:30, and a meeting she accepted 10–11:30 (back to back with it).
        await BookAsAsync(s.Rana, new CreateBookingDto
        {
            SpaceId = s.OtherRoom, LocalStart = Tomorrow.AddHours(8), LocalEnd = Tomorrow.AddHours(10.5), 
            IdempotencyKey = Guid.NewGuid().ToString(),
        });
        var meeting = await BookAsAsync(s.Lina, new CreateBookingDto
        {
            SpaceId = s.Room, LocalStart = Tomorrow.AddHours(10), LocalEnd = Tomorrow.AddHours(11.5), 
            IdempotencyKey = Guid.NewGuid().ToString(), Invitees = [new InviteeDto { UserId = s.Rana }],
        });
        await AnswerAsync(meeting.Id, s.Rana, InviteeResponseStatus.Accepted);

        using var _ = ActAs(s.Owner);
        var preview = await _bookings.PreviewAsync(new CreateBookingDto
        {
            SpaceId = s.OtherRoom, LocalStart = Tomorrow.AddHours(10.5), LocalEnd = Tomorrow.AddHours(12), 
            IdempotencyKey = Guid.NewGuid().ToString(), Invitees = [new InviteeDto { UserId = s.Rana }],
        });

        // 08–10:30 only touches the slot (10:30–12), so it's not there; the meeting is cut to 10:30–11:30.
        Times(preview, s.Rana).ShouldBe(new[] { (Tomorrow.AddHours(10.5), Tomorrow.AddHours(11.5)) });
    }

    [Fact]
    public async Task A_pending_or_declined_invite_a_cancelled_booking_or_a_touching_one_is_not_busy()
    {
        var s = await CreateScenarioAsync();
        var pending = await BookAsAsync(s.Lina, Booking(s.OtherRoom, 10, 11, 3, s.Rana, s.Omar));
        await AnswerAsync(pending.Id, s.Omar, InviteeResponseStatus.Declined);
        var cancelled = await BookAsAsync(s.Lina, Booking(s.OtherRoom, 12, 13));
        using (ActAs(s.Lina))
        {
            await _bookings.CancelAsync(cancelled.Id, new CancelBookingDto());
        }

        await BookAsAsync(s.Stranger, Booking(s.OtherRoom, 14, 15));

        using var _ = ActAs(s.Owner);
        // 10–11: Rana pending, Omar declined. 12–13: Lina's cancelled. 15–16 only touches the Stranger's 14–15.
        BusyIn(await _bookings.PreviewAsync(Booking(s.Room, 10, 11, 3, s.Rana, s.Omar)), s.Rana).ShouldBeFalse();
        BusyIn(await _bookings.PreviewAsync(Booking(s.Room, 10, 11, 3, s.Rana, s.Omar)), s.Omar).ShouldBeFalse();
        BusyIn(await _bookings.PreviewAsync(Booking(s.Room, 12, 13, 2, s.Lina)), s.Lina).ShouldBeFalse();
        BusyIn(await _bookings.PreviewAsync(Booking(s.Room, 15, 16, 2, s.Stranger)), s.Stranger).ShouldBeFalse();
    }

    [Fact]
    public async Task A_series_counts_the_dates_a_colleague_is_busy_on()
    {
        var s = await CreateScenarioAsync();
        // Rana is booked at 10:00 on the 1st and 3rd of the four dates.
        await BookAsAsync(s.Rana, Booking(s.OtherRoom, 10, 11));
        await BookAsAsync(s.Rana, new CreateBookingDto
        {
            SpaceId = s.OtherRoom, LocalStart = Tomorrow.AddDays(2).AddHours(10), LocalEnd = Tomorrow.AddDays(2).AddHours(11),
            IdempotencyKey = Guid.NewGuid().ToString(),
        });

        using var _ = ActAs(s.Owner);
        var preview = await _bookings.PreviewSeriesAsync(new SeriesRequestDto
        {
            SpaceId = s.Room,
            LocalStart = Tomorrow.AddHours(10),
            LocalEnd = Tomorrow.AddHours(11),
            Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(3)) },
            Invitees = [new InviteeDto { UserId = s.Rana }, new InviteeDto { UserId = s.Omar }],
        });

        var rana = preview.Invitees.Single(i => i.UserId == s.Rana);
        rana.IsBusy.ShouldBeTrue();
        rana.BusyDates.ShouldBe(2);
        rana.BusyTimes.Select(t => t.LocalStart).ShouldBe(new[] { Tomorrow.AddHours(10), Tomorrow.AddDays(2).AddHours(10) });
        preview.Invitees.Single(i => i.UserId == s.Omar).BusyDates.ShouldBe(0);
        preview.Occurrences.Count.ShouldBe(4);
    }

    // ---- Edit guests ----

    [Fact]
    public async Task Edit_guests_asks_who_is_busy_without_the_booking_itself_counting()
    {
        var s = await CreateScenarioAsync();
        var mine = await BookAsAsync(s.Owner, Booking(s.Room, 10, 11, 3, s.Rana));
        await AnswerAsync(mine.Id, s.Rana, InviteeResponseStatus.Accepted);
        await BookAsAsync(s.Omar, Booking(s.OtherRoom, 10, 11));

        using var _ = ActAs(s.Owner);
        var busy = await _bookings.GetBusyGuestsAsync(mine.Id, new BusyGuestsInput { UserIds = [s.Rana, s.Omar, s.Lina] });

        // Rana accepted this very meeting: not "busy" with it. Omar has his own room then.
        busy.Dates.ShouldBe(1);
        busy.Items.Select(b => (b.UserId, b.BusyDates)).ShouldBe(new[] { (s.Omar, 1) });
        busy.Items[0].Times.Select(t => (t.LocalStart, t.LocalEnd)).ShouldBe(new[] { (Tomorrow.AddHours(10), Tomorrow.AddHours(11)) });
    }

    [Fact]
    public async Task Edit_guests_on_a_series_counts_busy_upcoming_dates()
    {
        var s = await CreateScenarioAsync();
        SeriesCreatedDto series;
        using (ActAs(s.Owner))
        {
            series = await _bookings.CreateSeriesAsync(new CreateSeriesDto
            {
                SpaceId = s.Room,
                LocalStart = Tomorrow.AddHours(10),
                LocalEnd = Tomorrow.AddHours(11),
                Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(2)) },
                IdempotencyKey = Guid.NewGuid().ToString()[..30],
                Invitees = [new InviteeDto { UserId = s.Rana }],
            });
        }

        await BookAsAsync(s.Omar, Booking(s.OtherRoom, 10, 11));

        using var _ = ActAs(s.Owner);
        var busy = await _bookings.GetSeriesBusyGuestsAsync(series.SeriesId, new BusyGuestsInput { UserIds = [s.Rana, s.Omar] });

        busy.Dates.ShouldBe(3);
        busy.Items.Select(b => (b.UserId, b.BusyDates)).ShouldBe(new[] { (s.Omar, 1) });
    }

    [Fact]
    public async Task Only_the_organiser_asks_who_is_busy()
    {
        var s = await CreateScenarioAsync();
        var mine = await BookAsAsync(s.Owner, Booking(s.Room, 10, 11, 2, s.Rana));
        var input = new BusyGuestsInput { UserIds = [s.Omar] };

        using (ActAs(s.Rana))
        {
            (await Should.ThrowAsync<BusinessException>(() => _bookings.GetBusyGuestsAsync(mine.Id, input)))
                .Code.ShouldBe(DixelsDomainErrorCodes.BookingOrganiserOnly);
        }

        using (ActAs(s.Stranger))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _bookings.GetBusyGuestsAsync(mine.Id, input));
        }
    }

    // ---- A guest about to answer ----

    [Fact]
    public async Task A_guest_reading_an_invite_sees_when_they_are_already_taken_then()
    {
        var s = await CreateScenarioAsync();
        var invite = await BookAsAsync(s.Owner, Booking(s.Room, 10, 11, 2, s.Rana));
        // Rana's own room 10:30–11:30, and a meeting she accepted 09:00–10:15.
        await BookAsAsync(s.Rana, new CreateBookingDto
        {
            SpaceId = s.OtherRoom, LocalStart = Tomorrow.AddHours(10.5), LocalEnd = Tomorrow.AddHours(11.5), 
            IdempotencyKey = Guid.NewGuid().ToString(),
        });
        var earlier = await BookAsAsync(s.Lina, new CreateBookingDto
        {
            SpaceId = s.OtherRoom, LocalStart = Tomorrow.AddHours(9), LocalEnd = Tomorrow.AddHours(10.25), 
            IdempotencyKey = Guid.NewGuid().ToString(), Invitees = [new InviteeDto { UserId = s.Rana }],
        });
        await AnswerAsync(earlier.Id, s.Rana, InviteeResponseStatus.Accepted);

        using (ActAs(s.Rana))
        {
            // Cut to the invite's 10–11, and the invite itself doesn't count.
            (await _bookings.GetAsync(invite.Id)).MyBusy.Select(t => (t.LocalStart, t.LocalEnd)).ShouldBe(new[]
            {
                (Tomorrow.AddHours(10), Tomorrow.AddHours(10.25)),
                (Tomorrow.AddHours(10.5), Tomorrow.AddHours(11)),
            });
        }

        using (ActAs(s.Owner))
        {
            (await _bookings.GetAsync(invite.Id)).MyBusy.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task A_free_guest_or_one_reading_a_cancelled_invite_sees_nothing_busy()
    {
        var s = await CreateScenarioAsync();
        var invite = await BookAsAsync(s.Owner, Booking(s.Room, 10, 11, 2, s.Rana));

        using (ActAs(s.Rana))
        {
            (await _bookings.GetAsync(invite.Id)).MyBusy.ShouldBeEmpty();
        }

        await BookAsAsync(s.Rana, Booking(s.OtherRoom, 10, 11));
        using (ActAs(s.Owner))
        {
            await _bookings.CancelAsync(invite.Id, new CancelBookingDto());
        }

        using (ActAs(s.Rana))
        {
            (await _bookings.GetAsync(invite.Id)).MyBusy.ShouldBeEmpty();
        }
    }

    // ---- My own booking over a meeting I accepted ----

    [Theory]
    [InlineData(OwnOverlapPolicy.Warn)]
    [InlineData(OwnOverlapPolicy.Block)]
    public async Task Booking_over_a_meeting_I_accepted_is_a_heads_up_never_a_refusal(OwnOverlapPolicy policy)
    {
        var s = await CreateScenarioAsync(policy);
        var meeting = await BookAsAsync(s.Rana, Booking(s.OtherRoom, 10, 11, 2, s.Owner));
        await AnswerAsync(meeting.Id, s.Owner, InviteeResponseStatus.Accepted);

        using var _ = ActAs(s.Owner);
        var preview = await _bookings.PreviewAsync(Booking(s.Room, 10, 11));

        preview.IsValid.ShouldBeTrue();
        var warning = preview.Warnings.ShouldHaveSingleItem();
        warning.Code.ShouldBe(DixelsDomainErrorCodes.BookingAcceptedMeetingOverlapWarning);
        warning.Message.ShouldContain("you accepted a meeting");
        (await _bookings.CreateAsync(Booking(s.Room, 10, 11))).Status.ShouldBe("Confirmed");
    }

    [Fact]
    public async Task A_pending_invite_or_a_building_that_allows_double_bookings_gives_no_heads_up()
    {
        var pendingScenario = await CreateScenarioAsync();
        await BookAsAsync(pendingScenario.Rana, Booking(pendingScenario.OtherRoom, 10, 11, 2, pendingScenario.Owner));
        using (ActAs(pendingScenario.Owner))
        {
            (await _bookings.PreviewAsync(Booking(pendingScenario.Room, 10, 11))).Warnings.ShouldBeEmpty();
        }

        var allow = await CreateScenarioAsync(OwnOverlapPolicy.Allow);
        var meeting = await BookAsAsync(allow.Rana, Booking(allow.OtherRoom, 10, 11, 2, allow.Owner));
        await AnswerAsync(meeting.Id, allow.Owner, InviteeResponseStatus.Accepted);
        using (ActAs(allow.Owner))
        {
            (await _bookings.PreviewAsync(Booking(allow.Room, 10, 11))).Warnings.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task A_series_warns_on_each_date_with_an_accepted_meeting()
    {
        var s = await CreateScenarioAsync(OwnOverlapPolicy.Block);
        var meeting = await BookAsAsync(s.Rana, new CreateBookingDto
        {
            SpaceId = s.OtherRoom, LocalStart = Tomorrow.AddDays(1).AddHours(10), LocalEnd = Tomorrow.AddDays(1).AddHours(11),
            IdempotencyKey = Guid.NewGuid().ToString(), Invitees = [new InviteeDto { UserId = s.Owner }],
        });
        await AnswerAsync(meeting.Id, s.Owner, InviteeResponseStatus.Accepted);

        using var _ = ActAs(s.Owner);
        var preview = await _bookings.PreviewSeriesAsync(new SeriesRequestDto
        {
            SpaceId = s.Room,
            LocalStart = Tomorrow.AddHours(10),
            LocalEnd = Tomorrow.AddHours(11),
            Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(2)) },
        });

        preview.BookableCount.ShouldBe(3);
        preview.Occurrences.Select(o => o.Warnings.Count).ShouldBe(new[] { 0, 1, 0 });
        preview.Occurrences[1].Warnings[0].Code.ShouldBe(DixelsDomainErrorCodes.BookingAcceptedMeetingOverlapWarning);
    }
}
