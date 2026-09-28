using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.Users;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace Dixels.Postgres.Bookings;

/// <summary>
/// The BRS technical acceptance criteria that only a real Postgres can prove: concurrent
/// overlapping requests are serialised so exactly one wins, the loser leaves nothing behind,
/// and the database itself (not just the application check) refuses a double booking.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BookingConcurrencyTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly IBookingsAppService _bookingsAppService;
    private readonly IBookingRepository _bookingRepository;

    public BookingConcurrencyTests()
    {
        _bookingsAppService = GetRequiredService<IBookingsAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
    }

    private sealed record Scenario(Guid UserId, Guid SpaceId, Guid FloorId);

    // Every test makes its own building, space and employee: the container's database is
    // shared by all tests in the run.
    private Task<Scenario> CreateScenarioAsync(OwnOverlapPolicy policy = OwnOverlapPolicy.Warn) => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "PG HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0, ownOverlapPolicy: policy));

        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "Room", spaceType.Id, 8));

        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        user.SetBuildingId(building.Id);
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, space.Id, floor.Id);
    });

    private Task<Guid> AddSpaceAsync(Scenario s, string name) => WithUnitOfWorkAsync(async () =>
    {
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        return (await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), s.FloorId, name, spaceType.Id, 8))).Id;
    });

    private IDisposable ActAs(Guid userId) =>
        GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
        })));

    /// <summary>A booking attempt in its own transactional unit of work — like one HTTP request.</summary>
    private Task<BookingDto> BookInOwnTransactionAsync(Guid spaceId, int startHour, int endHour) =>
        WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true), () => _bookingsAppService.CreateAsync(new CreateBookingDto
        {
            SpaceId = spaceId,
            LocalStart = Tomorrow.AddHours(startHour),
            LocalEnd = Tomorrow.AddHours(endHour),
            Attendees = 1,
            IdempotencyKey = Guid.NewGuid().ToString(),
        }));

    private Task<int> CountConfirmedAsync(Guid spaceId) =>
        WithUnitOfWorkAsync(() => _bookingRepository.CountAsync(b => b.SpaceId == spaceId && b.Status == BookingStatus.Confirmed));

    private Booking NewBooking(Scenario s, int startHour, int endHour) =>
        new(Guid.NewGuid(), s.SpaceId, s.UserId,
            new DateTimeOffset(Tomorrow.AddHours(startHour), TimeSpan.Zero),
            new DateTimeOffset(Tomorrow.AddHours(endHour), TimeSpan.Zero),
            attendees: 1, "Direct", "{}", Guid.NewGuid().ToString());

    [PostgresFact]
    public async Task Simultaneous_overlapping_requests_confirm_exactly_one()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        // Ten requests for overlapping windows, released at the same moment.
        var attempts = Enumerable.Range(0, 10)
            .Select(i => Task.Run(() => BookInOwnTransactionAsync(s.SpaceId, 10, 12 + i % 2)))
            .ToList();

        var outcomes = await Task.WhenAll(attempts.Select(async t =>
        {
            try
            {
                await t;
                return (Won: true, Code: (string?)null);
            }
            catch (BusinessException ex)
            {
                return (Won: false, Code: ex.Code);
            }
        }));

        outcomes.Count(o => o.Won).ShouldBe(1);
        outcomes.Where(o => !o.Won).ShouldAllBe(o => o.Code == DixelsDomainErrorCodes.BookingOverlap);

        // The losers rolled back completely — no partial rows.
        (await CountConfirmedAsync(s.SpaceId)).ShouldBe(1);
    }

    [PostgresFact]
    public async Task One_booking_at_a_time_holds_across_rooms_even_when_requests_race()
    {
        var s = await CreateScenarioAsync(OwnOverlapPolicy.Block);
        var rooms = new[] { s.SpaceId, await AddSpaceAsync(s, "Room B"), await AddSpaceAsync(s, "Room C"), await AddSpaceAsync(s, "Room D") };
        using var _ = ActAs(s.UserId);

        // The same person, four tabs, four different rooms, the same hour — released at once.
        // No room clashes with another, so only the per-person lock can keep this to one.
        var outcomes = await Task.WhenAll(rooms.Select(room => Task.Run(async () =>
        {
            try
            {
                await BookInOwnTransactionAsync(room, 10, 11);
                return (Won: true, Code: (string?)null);
            }
            catch (BusinessException ex)
            {
                return (Won: false, Code: ex.Code);
            }
        })));

        outcomes.Count(o => o.Won).ShouldBe(1);
        outcomes.Where(o => !o.Won).ShouldAllBe(o => o.Code == DixelsDomainErrorCodes.BookingOwnOverlap);
    }

    [PostgresFact]
    public async Task Two_series_racing_for_the_same_room_never_leave_a_half_booked_one()
    {
        var s = await CreateScenarioAsync(OwnOverlapPolicy.Allow);
        using var _ = ActAs(s.UserId);

        // Both want 10:00–11:00 every day this week in the same room, released at once.
        Task<SeriesCreatedDto> Attempt() => WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true), () =>
            _bookingsAppService.CreateSeriesAsync(new CreateSeriesDto
            {
                SpaceId = s.SpaceId,
                LocalStart = Tomorrow.AddHours(10),
                LocalEnd = Tomorrow.AddHours(11),
                Attendees = 1,
                Recurrence = new RecurrenceDto
                {
                    Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(4)),
                },
                IdempotencyKey = Guid.NewGuid().ToString(),
            }));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            try
            {
                return (Won: true, Code: (string?)null, Count: (await Attempt()).Bookings.Count);
            }
            catch (BusinessException ex)
            {
                return (Won: false, Code: ex.Code, Count: 0);
            }
        })));

        outcomes.Count(o => o.Won).ShouldBe(1);
        outcomes.Single(o => o.Won).Count.ShouldBe(5);
        outcomes.Single(o => !o.Won).Code.ShouldBeOneOf(DixelsDomainErrorCodes.SeriesDateUnavailable, DixelsDomainErrorCodes.BookingOverlap);
        (await CountConfirmedAsync(s.SpaceId)).ShouldBe(5);
    }

    [PostgresFact]
    public async Task The_database_refuses_an_overlap_even_when_the_application_check_is_bypassed()
    {
        var s = await CreateScenarioAsync();

        await WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true),
            () => _bookingRepository.InsertConfirmedAsync(NewBooking(s, 10, 12)));

        // Straight to the repository: no lock, no validator. Only the exclusion constraint
        // stands in the way.
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true),
                () => _bookingRepository.InsertConfirmedAsync(NewBooking(s, 11, 13))));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.BookingOverlap);
        (await CountConfirmedAsync(s.SpaceId)).ShouldBe(1);
    }

    [PostgresFact]
    public async Task The_database_accepts_back_to_back_bookings()
    {
        var s = await CreateScenarioAsync();

        await WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true),
            () => _bookingRepository.InsertConfirmedAsync(NewBooking(s, 10, 11)));
        await WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true),
            () => _bookingRepository.InsertConfirmedAsync(NewBooking(s, 11, 12)));

        (await CountConfirmedAsync(s.SpaceId)).ShouldBe(2);
    }

    [PostgresFact]
    public async Task The_database_lets_a_cancelled_booking_be_rebooked()
    {
        var s = await CreateScenarioAsync();
        var first = NewBooking(s, 10, 11);

        await WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true),
            () => _bookingRepository.InsertConfirmedAsync(first));

        await WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true), async () =>
        {
            var stored = await _bookingRepository.GetAsync(first.Id);
            stored.Cancel(s.UserId, DateTimeOffset.UtcNow, null, byAdmin: false);
            await _bookingRepository.UpdateAsync(stored);
        });

        await WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true),
            () => _bookingRepository.InsertConfirmedAsync(NewBooking(s, 10, 11)));

        (await CountConfirmedAsync(s.SpaceId)).ShouldBe(1);
    }

    [PostgresFact]
    public async Task Times_round_trip_as_utc()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        var created = await BookInOwnTransactionAsync(s.SpaceId, 10, 11);
        var stored = await WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(created.Id));

        stored.StartsAt.ShouldBe(new DateTimeOffset(Tomorrow.AddHours(10), TimeSpan.Zero));
        stored.StartsAt.Offset.ShouldBe(TimeSpan.Zero);
    }
}
