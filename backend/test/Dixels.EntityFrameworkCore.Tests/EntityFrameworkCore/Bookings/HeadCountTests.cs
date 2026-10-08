using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// The head count is the booker plus the guests, worked out by the server from the guest list as
/// it resolves it: nobody types it. A room's capacity counts the guests, and its minimum is met by
/// inviting people.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class HeadCountTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly IBookingsAppService _bookings;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;

    public HeadCountTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
    }

    private sealed record Person(Guid Id, string Email);

    /// <summary>A UTC, 24/7 building with a 4-seat room; the owner and three colleagues work there.</summary>
    private sealed record Scenario(Guid SpaceId, Person Owner, Person Rana, Person Omar, Person Lina);

    private Task<Scenario> CreateScenarioAsync(int? minAttendees = null) => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, capacity: 4);
        if (minAttendees is { } min)
        {
            space.SetMinAttendees(min);
        }

        await _spaceRepository.InsertAsync(space);

        async Task<Person> NewUserAsync(string name)
        {
            var key = Guid.NewGuid().ToString("N")[..8];
            var user = new IdentityUser(Guid.NewGuid(), "u" + key, $"{name.ToLowerInvariant()}.{key}@test.io") { Name = name };
            user.SetBuildingId(building.Id);
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return new Person(user.Id, user.Email);
        }

        return new Scenario(space.Id, await NewUserAsync("Owner"), await NewUserAsync("Rana"), await NewUserAsync("Omar"), await NewUserAsync("Lina"));
    });

    private IDisposable ActAs(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
        new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private static InviteeDto Colleague(Person p) => new() { UserId = p.Id };

    // Each request an hour of its own, so a test's bookings never clash.
    private int _nextHour = 8;

    private CreateBookingDto Request(Scenario s, params InviteeDto[] invitees) => new()
    {
        SpaceId = s.SpaceId,
        LocalStart = Tomorrow.AddHours(_nextHour),
        LocalEnd = Tomorrow.AddHours(++_nextHour),
        IdempotencyKey = Guid.NewGuid().ToString(),
        Invitees = invitees.ToList(),
    };

    private async Task<BookingDto> BookAsync(Scenario s, params InviteeDto[] invitees)
    {
        using var _ = ActAs(s.Owner.Id);
        return await _bookings.CreateAsync(Request(s, invitees));
    }

    [Fact]
    public async Task The_head_count_is_the_booker_plus_the_guests()
    {
        var s = await CreateScenarioAsync();

        (await BookAsync(s)).Attendees.ShouldBe(1);

        var booking = await BookAsync(s, Colleague(s.Rana), new InviteeDto { Email = "sam@outside.io" });
        booking.Attendees.ShouldBe(3);
        (await WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(booking.Id))).Attendees.ShouldBe(3);
    }

    [Fact]
    public async Task A_typed_colleague_email_counts_once_and_the_same_person_twice_is_refused()
    {
        var s = await CreateScenarioAsync();

        // Typed in by email, it's the colleague: one person.
        using (ActAs(s.Owner.Id))
        {
            var preview = await _bookings.PreviewAsync(Request(s, new InviteeDto { Email = s.Rana.Email.ToUpperInvariant() }));
            preview.Invitees.ShouldHaveSingleItem().UserId.ShouldBe(s.Rana.Id);
            preview.IsValid.ShouldBeTrue();
        }

        (await BookAsync(s, new InviteeDto { Email = s.Rana.Email })).Attendees.ShouldBe(2);

        // Picked by name as well: refused, never counted twice.
        using (ActAs(s.Owner.Id))
        {
            (await Should.ThrowAsync<BusinessException>(() => _bookings.CreateAsync(Request(s, Colleague(s.Rana), new InviteeDto { Email = s.Rana.Email }))))
                .Code.ShouldBe(DixelsDomainErrorCodes.InviteeDuplicate);
        }
    }

    [Fact]
    public async Task The_room_s_capacity_counts_the_guests()
    {
        var s = await CreateScenarioAsync();
        var five = new[] { Colleague(s.Rana), Colleague(s.Omar), Colleague(s.Lina), new InviteeDto { Email = "sam@outside.io" } };

        using var _ = ActAs(s.Owner.Id);
        var preview = await _bookings.PreviewAsync(Request(s, five));

        var violation = preview.Violations.ShouldHaveSingleItem();
        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingOverCapacity);
        violation.Message.ShouldBe("This space seats 4, but with your guests you're 5 people. Invite fewer people or pick a larger space.");
    }

    [Fact]
    public async Task A_room_with_a_minimum_is_met_by_inviting_people()
    {
        var s = await CreateScenarioAsync(minAttendees: 3);

        using (ActAs(s.Owner.Id))
        {
            var alone = await _bookings.PreviewAsync(Request(s, Colleague(s.Rana)));
            alone.Violations.ShouldHaveSingleItem().Message
                .ShouldBe("This space needs at least 3 people (Space rule): invite 1 more — you count as one — or pick a smaller space.");
        }

        (await BookAsync(s, Colleague(s.Rana), Colleague(s.Omar))).Attendees.ShouldBe(3);
    }

    [Fact]
    public async Task Editing_guests_moves_the_head_count_with_them()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, Colleague(s.Rana));

        using var _ = ActAs(s.Owner.Id);
        (await _bookings.UpdateInviteesAsync(booking.Id, new UpdateInviteesDto { Invitees = new() { Colleague(s.Rana), Colleague(s.Omar) } }))
            .Attendees.ShouldBe(3);
        (await _bookings.UpdateInviteesAsync(booking.Id, new UpdateInviteesDto { Invitees = new() }))
            .Attendees.ShouldBe(1);
    }

    [Fact]
    public async Task A_series_counts_its_guests_on_every_date_and_an_edit_moves_them_all()
    {
        var s = await CreateScenarioAsync();

        SeriesCreatedDto series;
        using (ActAs(s.Owner.Id))
        {
            series = await _bookings.CreateSeriesAsync(new CreateSeriesDto
            {
                SpaceId = s.SpaceId,
                LocalStart = Tomorrow.AddHours(12),
                LocalEnd = Tomorrow.AddHours(13),
                Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(2)) },
                IdempotencyKey = Guid.NewGuid().ToString(),
                Invitees = new() { Colleague(s.Rana) },
            });
            series.Bookings.ShouldAllBe(b => b.Attendees == 2);

            var edited = await _bookings.UpdateSeriesInviteesAsync(series.SeriesId,
                new UpdateInviteesDto { Invitees = new() { Colleague(s.Rana), Colleague(s.Omar), Colleague(s.Lina) } });
            edited.Bookings.ShouldAllBe(b => b.Attendees == 4);
        }
    }

    [Fact]
    public async Task A_booking_that_counted_unnamed_people_drops_to_the_booker_plus_guests_on_its_next_edit()
    {
        var s = await CreateScenarioAsync();
        // From before the head count followed the guests: four people, one of them named.
        var id = await WithUnitOfWorkAsync(async () =>
        {
            var old = new Booking(Guid.NewGuid(), s.SpaceId, s.Owner.Id,
                new DateTimeOffset(Tomorrow.AddHours(15), TimeSpan.Zero), new DateTimeOffset(Tomorrow.AddHours(16), TimeSpan.Zero),
                attendees: 4, "Old", "{}", Guid.NewGuid().ToString());
            old.SetInvitees(new[] { new Invitee(s.Rana.Id, null, null) }, GetRequiredService<IGuidGenerator>());
            await _bookingRepository.InsertAsync(old);
            return old.Id;
        });

        using var _ = ActAs(s.Owner.Id);
        (await _bookings.UpdateInviteesAsync(id, new UpdateInviteesDto { Invitees = new() { Colleague(s.Rana), Colleague(s.Omar) } }))
            .Attendees.ShouldBe(3);
    }

    [Fact]
    public async Task A_kept_booking_in_a_room_that_shrank_can_lose_guests_but_not_grow()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, Colleague(s.Rana), Colleague(s.Omar), Colleague(s.Lina));
        await WithUnitOfWorkAsync(async () =>
        {
            var room = await _spaceRepository.GetAsync(s.SpaceId);
            room.SetCapacity(2);
            await _spaceRepository.UpdateAsync(room);
        });

        using var _ = ActAs(s.Owner.Id);
        // Down from 4 to 3: still over the new capacity, but it's going the right way.
        (await _bookings.UpdateInviteesAsync(booking.Id, new UpdateInviteesDto { Invitees = new() { Colleague(s.Rana), Colleague(s.Omar) } }))
            .Attendees.ShouldBe(3);

        (await Should.ThrowAsync<BusinessException>(() => _bookings.UpdateInviteesAsync(booking.Id,
                new UpdateInviteesDto { Invitees = new() { Colleague(s.Rana), Colleague(s.Omar), Colleague(s.Lina) } })))
            .Code.ShouldBe(DixelsDomainErrorCodes.BookingOverCapacity);
    }
}
