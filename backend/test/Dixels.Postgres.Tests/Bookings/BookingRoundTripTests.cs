using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Npgsql;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;
using Xunit.Abstractions;

namespace Dixels.Postgres.Bookings;

/// <summary>
/// How many times the busiest booking paths go to the database: a create, the calendar, a
/// 100-date series. Each round trip costs latency however small the query, so these counts
/// are held down here (the email a create sends runs in the same request and is counted too).
/// </summary>
[Collection(PostgresCollection.Name)]
public class BookingRoundTripTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly IBookingsAppService _bookings;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ITestOutputHelper _output;

    public BookingRoundTripTests(ITestOutputHelper output)
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _output = output;
    }

    private sealed record Scenario(Guid UserId, Guid SpaceId);

    /// <summary>A UTC building open all day (series up to 120 days ahead), one room, and someone assigned to it.</summary>
    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Trips " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0, maxSeriesHorizonDays: 120));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, 8));

        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
        user.SetBuildingId(building.Id);
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, space.Id);
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private CreateBookingDto Request(Scenario s, int startHour, string? key = null) => new()
    {
        SpaceId = s.SpaceId,
        LocalStart = Tomorrow.AddHours(startHour),
        LocalEnd = Tomorrow.AddHours(startHour + 1),
        Attendees = 2,
        Title = "Planning",
        IdempotencyKey = key ?? Guid.NewGuid().ToString(),
    };

    /// <summary>The commands captured since the last clear, each shown by its first line.</summary>
    private List<NpgsqlCommand> Captured(string what)
    {
        var commands = SqlCapture.Instance.Commands.ToList();
        _output.WriteLine($"{what}: {commands.Count} commands");
        foreach (var command in commands)
        {
            _output.WriteLine("  " + command.CommandText.Split('\n')[0].Trim());
        }

        return commands;
    }

    [PostgresFact]
    public async Task A_create_goes_to_the_database_a_bounded_number_of_times()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        SqlCapture.Instance.Clear();
        var created = await _bookings.CreateAsync(Request(s, 10));
        var commands = Captured("create");

        created.SpaceName.ShouldBe("Room 1");
        created.FloorName.ShouldBe("Level 1");
        created.BuildingName.ShouldStartWith("PG Trips");
        // 35 on main: the room, floor and building were loaded again (2 commands each) to describe the result.
        commands.Count.ShouldBeLessThanOrEqualTo(29);
    }

    [PostgresFact]
    public async Task The_calendar_goes_to_the_database_a_bounded_number_of_times()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);
        await _bookings.CreateAsync(Request(s, 10));
        await _bookings.CreateAsync(Request(s, 13));

        SqlCapture.Instance.Clear();
        var calendar = await _bookings.GetMineAsync(new GetMyBookingsInput { From = Tomorrow, To = Tomorrow.AddDays(1) });
        var commands = Captured("calendar");
        foreach (var command in commands)
        {
            _output.WriteLine(command.CommandText);
        }

        calendar.Items.Select(b => b.SpaceName).ShouldBe(new[] { "Room 1", "Room 1" });
        // 10 before #43 (whole floors and buildings with their names, for one timezone each),
        // 7 before the user's own building was read as just its timezone.
        commands.Count.ShouldBeLessThanOrEqualTo(6);
    }

    [PostgresFact]
    public async Task A_hundred_date_series_is_saved_in_few_round_trips()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.UserId);

        SqlCapture.Instance.Clear();
        var created = await _bookings.CreateSeriesAsync(new CreateSeriesDto
        {
            SpaceId = s.SpaceId,
            LocalStart = Tomorrow.AddHours(9),
            LocalEnd = Tomorrow.AddHours(10),
            Attendees = 2,
            Title = "Stand-up",
            Recurrence = new RecurrenceDto
            {
                Frequency = RecurrenceFrequency.Daily,
                EndDate = DateOnly.FromDateTime(Tomorrow).AddDays(BookingConsts.MaxSeriesOccurrences - 1),
            },
            IdempotencyKey = Guid.NewGuid().ToString("N")[..20],
        });
        var commands = Captured("series");
        var bookingInserts = commands.Count(c => c.CommandText.Contains("INSERT INTO \"AppBookings\""));
        _output.WriteLine($"booking INSERT commands: {bookingInserts}");

        created.Bookings.Count.ShouldBe(BookingConsts.MaxSeriesOccurrences);
        created.Bookings.ShouldAllBe(b => b.SpaceName == "Room 1" && b.FloorName == "Level 1");
        // 100 on main: one insert and save per date.
        bookingInserts.ShouldBeLessThanOrEqualTo(MostSeriesInsertCommands);
    }

    // EF batches the rows of one save into a few commands; however many, far fewer than the dates.
    private const int MostSeriesInsertCommands = 5;

    [PostgresFact]
    public async Task A_batch_with_one_clashing_date_saves_none_and_says_already_booked()
    {
        // The race the locks didn't cover: a date taken after the checks. The database's
        // no-overlap rule rejects that row, and with it the whole batch.
        var s = await CreateScenarioAsync();
        var repository = GetRequiredService<IBookingRepository>();
        var at = new DateTimeOffset(Tomorrow, TimeSpan.Zero);
        await WithUnitOfWorkAsync(() => repository.InsertAsync(new Booking(
            Guid.NewGuid(), s.SpaceId, s.UserId, at.AddDays(1).AddHours(9), at.AddDays(1).AddHours(10),
            attendees: 1, "Taken", "{}", Guid.NewGuid().ToString())));

        var batch = Enumerable.Range(0, 3).Select(day => new Booking(
                Guid.NewGuid(), s.SpaceId, s.UserId, at.AddDays(day).AddHours(9), at.AddDays(day).AddHours(10),
                attendees: 1, "Series", "{}", Guid.NewGuid().ToString()))
            .ToList();

        var error = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            WithUnitOfWorkAsync(() => repository.InsertManyConfirmedAsync(batch)));

        error.Code.ShouldBe(DixelsDomainErrorCodes.BookingOverlap);
        var saved = await WithUnitOfWorkAsync(() => repository.GetListAsync(b => b.SpaceId == s.SpaceId));
        saved.ShouldHaveSingleItem().Title.ShouldBe("Taken");
    }
}
