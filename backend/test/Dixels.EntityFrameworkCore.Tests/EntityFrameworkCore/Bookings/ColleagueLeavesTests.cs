using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// A colleague guest who is deactivated, removed or moved to another building comes off the
/// upcoming meetings they were invited to (in the building they left, for a move), silently
/// and off the head count. Meetings under way, over or cancelled keep them.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class ColleagueLeavesTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookings;
    private readonly IUsersAppService _users;
    private readonly IIdentityUserAppService _identityUsers;
    private readonly IBookingRepository _bookingRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);
    private static readonly Guid Admin = Guid.NewGuid();

    public ColleagueLeavesTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _users = GetRequiredService<IUsersAppService>();
        _identityUsers = GetRequiredService<IIdentityUserAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    /// <summary>Two UTC, 24/7 buildings with a room each; the owner, Rana and Omar work in the first.</summary>
    private sealed record Scenario(Guid BuildingA, Guid RoomA, Guid BuildingB, Guid RoomB, Guid Owner, Guid Rana, Guid Omar);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();

        async Task<(Guid Building, Guid Room)> NewBuildingAsync()
        {
            var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
                Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
                new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
                maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
            var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, capacity: 8));
            return (building.Id, space.Id);
        }

        var (buildingA, roomA) = await NewBuildingAsync();
        var (buildingB, roomB) = await NewBuildingAsync();

        async Task<Guid> NewUserAsync(string name)
        {
            var key = Guid.NewGuid().ToString("N")[..8];
            var user = new IdentityUser(Guid.NewGuid(), "u" + key, $"{name.ToLowerInvariant()}.{key}@test.io") { Name = name };
            user.SetBuildingId(buildingA);
            (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return user.Id;
        }

        return new Scenario(buildingA, roomA, buildingB, roomB, await NewUserAsync("Owner"), await NewUserAsync("Rana"), await NewUserAsync("Omar"));
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private static List<InviteeDto> Colleagues(params Guid[] ids) => ids.Select(id => new InviteeDto { UserId = id }).ToList();

    private async Task<BookingDto> BookAsync(Scenario s, int hour, params Guid[] colleagues)
    {
        using var _ = ActAs(s.Owner);
        return await _bookings.CreateAsync(new CreateBookingDto
        {
            SpaceId = s.RoomA,
            LocalStart = Tomorrow.AddHours(hour),
            LocalEnd = Tomorrow.AddHours(hour + 1),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Invitees = Colleagues(colleagues),
        });
    }

    private async Task<SeriesCreatedDto> BookSeriesAsync(Scenario s, int hour, int days, params Guid[] colleagues)
    {
        using var _ = ActAs(s.Owner);
        return await _bookings.CreateSeriesAsync(new CreateSeriesDto
        {
            SpaceId = s.RoomA,
            LocalStart = Tomorrow.AddHours(hour),
            LocalEnd = Tomorrow.AddHours(hour + 1),
            Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(days - 1)) },
            IdempotencyKey = Guid.NewGuid().ToString(),
            Invitees = Colleagues(colleagues),
        });
    }

    /// <summary>
    /// A booking stored as it stands, past the app's rules: one that's over or under way, a
    /// series' past date, or one in a building the guest doesn't work in.
    /// </summary>
    private Task<Guid> InsertAsync(Scenario s, Guid roomId, DateTimeOffset start, Guid[] colleagues, Guid? seriesId = null, bool cancelled = false) =>
        WithUnitOfWorkAsync(async () =>
        {
            var booking = new Booking(Guid.NewGuid(), roomId, s.Owner, start, start.AddHours(1), 1 + colleagues.Length,
                "Direct", "{}", Guid.NewGuid().ToString(), seriesId);
            booking.SetInvitees(colleagues.Select(id => new Invitee(id, null, null)).ToList(), GetRequiredService<IGuidGenerator>());
            if (cancelled)
            {
                booking.Cancel(s.Owner, DateTimeOffset.UtcNow, reason: null, byAdmin: false);
            }

            await _bookingRepository.InsertAsync(booking);
            return booking.Id;
        });

    private Task<DixelsDbContext> DbContextAsync() =>
        GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();

    private Task<List<Guid?>> GuestsOfAsync(Guid bookingId) => WithUnitOfWorkAsync(async () =>
        await (await DbContextAsync()).Set<BookingAttendee>().Where(a => a.BookingId == bookingId).Select(a => a.UserId).ToListAsync());

    private Task<List<Guid?>> SeriesGuestsAsync(Guid seriesId) => WithUnitOfWorkAsync(async () =>
        await (await DbContextAsync()).Set<BookingSeriesAttendee>().Where(a => a.SeriesId == seriesId).Select(a => a.UserId).ToListAsync());

    private Task AcceptAsync(Guid bookingId, Guid userId) => WithUnitOfWorkAsync(async () =>
    {
        var dbContext = await DbContextAsync();
        var row = await dbContext.Set<BookingAttendee>().SingleAsync(a => a.BookingId == bookingId && a.UserId == userId);
        dbContext.Entry(row).Property(nameof(BookingAttendee.ResponseStatus)).CurrentValue = InviteeResponseStatus.Accepted;
        await dbContext.SaveChangesAsync();
    });

    private async Task DeactivateAsync(Guid userId)
    {
        using var _ = ActAs(Admin);
        var user = await _identityUsers.GetAsync(userId);
        var update = new IdentityUserUpdateDto
        {
            UserName = user.UserName,
            Email = user.Email,
            Name = user.Name,
            Surname = user.Surname,
            IsActive = false,
            LockoutEnabled = user.LockoutEnabled,
            ConcurrencyStamp = user.ConcurrencyStamp,
        };
        foreach (var (key, value) in user.ExtraProperties)
        {
            update.ExtraProperties[key] = value;
        }

        await _identityUsers.UpdateAsync(userId, update);
    }

    private Task PublishAsync<TEvent>(TEvent eventData)
        where TEvent : class =>
        WithUnitOfWorkAsync(() => GetRequiredService<ILocalEventBus>().PublishAsync(eventData));

    [Fact]
    public async Task A_deactivated_colleague_leaves_upcoming_meetings_and_the_rest_keep_them()
    {
        var s = await CreateScenarioAsync();
        var single = await BookAsync(s, 10, s.Rana, s.Omar);
        await AcceptAsync(single.Id, s.Omar);
        var series = await BookSeriesAsync(s, 12, days: 3, s.Rana, s.Omar);
        var pastDate = await InsertAsync(s, s.RoomA, new DateTimeOffset(Tomorrow.AddDays(-2).AddHours(12)), new[] { s.Rana, s.Omar }, series.SeriesId);
        var underWay = await InsertAsync(s, s.RoomA, DateTimeOffset.UtcNow.AddMinutes(-30), new[] { s.Rana });
        var cancelled = await InsertAsync(s, s.RoomA, new DateTimeOffset(Tomorrow.AddHours(15)), new[] { s.Rana }, cancelled: true);
        var heard = false;

        using (GetRequiredService<ILocalEventBus>().Subscribe<BookingInviteesChangedEvent>(_ => { heard = true; return Task.CompletedTask; }))
        {
            await DeactivateAsync(s.Rana);
        }

        // Off the upcoming single booking and its head count; Omar's answer kept.
        (await GuestsOfAsync(single.Id)).ShouldBe(new Guid?[] { s.Omar });
        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(single.Id));
        stored.Attendees.ShouldBe(2);
        stored.Invitees.Single().ResponseStatus.ShouldBe(InviteeResponseStatus.Accepted);

        // Off the series' own list and every upcoming date.
        (await SeriesGuestsAsync(series.SeriesId)).ShouldBe(new Guid?[] { s.Omar });
        series.Bookings.Count.ShouldBe(3);
        foreach (var date in series.Bookings)
        {
            (await GuestsOfAsync(date.Id)).ShouldBe(new Guid?[] { s.Omar });
        }

        // History stays as it was.
        (await GuestsOfAsync(pastDate)).ShouldBe(new Guid?[] { s.Rana, s.Omar }, ignoreOrder: true);
        (await GuestsOfAsync(underWay)).ShouldBe(new Guid?[] { s.Rana });
        (await GuestsOfAsync(cancelled)).ShouldBe(new Guid?[] { s.Rana });

        // Silent: no guests-changed event, so nobody is emailed.
        heard.ShouldBeFalse();
    }

    [Fact]
    public async Task A_removed_colleague_leaves_upcoming_meetings()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, s.Rana, s.Omar);

        using (ActAs(Admin))
        {
            await _identityUsers.DeleteAsync(s.Rana);
        }

        (await GuestsOfAsync(booking.Id)).ShouldBe(new Guid?[] { s.Omar });
    }

    [Fact]
    public async Task A_colleague_who_moves_leaves_only_the_old_buildings_meetings()
    {
        var s = await CreateScenarioAsync();
        var inA = await BookAsync(s, 10, s.Rana, s.Omar);
        var seriesInA = await BookSeriesAsync(s, 12, days: 2, s.Rana);
        // Can't happen through the app (a colleague guest works in the room's building), but cheap to rule out.
        var inB = await InsertAsync(s, s.RoomB, new DateTimeOffset(Tomorrow.AddHours(10)), new[] { s.Rana });

        using (ActAs(Admin))
        {
            await _users.AssignBuildingAsync(s.Rana, new AssignUserBuildingDto { BuildingId = s.BuildingB });
        }

        (await GuestsOfAsync(inA.Id)).ShouldBe(new Guid?[] { s.Omar });
        (await SeriesGuestsAsync(seriesInA.SeriesId)).ShouldBeEmpty();
        foreach (var date in seriesInA.Bookings)
        {
            (await GuestsOfAsync(date.Id)).ShouldBeEmpty();
        }

        (await GuestsOfAsync(inB)).ShouldBe(new Guid?[] { s.Rana });
    }

    [Fact]
    public async Task Moving_from_no_building_or_to_the_same_one_changes_nothing()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, s.Rana);

        using (ActAs(Admin))
        {
            await PublishAsync(new UserMovedBuildingEvent(s.Rana, null, s.BuildingA, Admin));
            await PublishAsync(new UserMovedBuildingEvent(s.Rana, s.BuildingA, s.BuildingA, Admin));
        }

        (await GuestsOfAsync(booking.Id)).ShouldBe(new Guid?[] { s.Rana });
    }

    [Fact]
    public async Task The_leavers_own_bookings_are_still_cancelled_alongside()
    {
        var s = await CreateScenarioAsync();
        var invited = await BookAsync(s, 10, s.Rana);
        BookingDto own;
        using (ActAs(s.Rana))
        {
            own = await _bookings.CreateAsync(new CreateBookingDto
            {
                SpaceId = s.RoomA,
                LocalStart = Tomorrow.AddHours(14),
                LocalEnd = Tomorrow.AddHours(15),
                IdempotencyKey = Guid.NewGuid().ToString(),
            });
        }

        await DeactivateAsync(s.Rana);

        (await WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(own.Id))).Status.ShouldBe(BookingStatus.Cancelled);
        (await GuestsOfAsync(invited.Id)).ShouldBeEmpty();
    }
}
