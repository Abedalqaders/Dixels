using System;
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
/// The guest tables on real Postgres: a series' guests saved for every date in its one save,
/// and the database itself refusing a row that's neither a colleague nor an email, or the same
/// colleague twice on one booking.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BookingInviteesPostgresTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly IBookingsAppService _bookings;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ITestOutputHelper _output;

    // 16 when written: loads, lock, guest check, the batched save and the reply. Not one per date.
    private const int BoundForSeriesEdit = 18;

    // Each person added or removed is emailed as the edit saves (E4): their user, language,
    // the room's names, the email job. About 11 each (22 for one in, one out), once per person —
    // never per date.
    private const int BoundPerEmailedGuest = 12;

    public BookingInviteesPostgresTests(ITestOutputHelper output)
    {
        _output = output;
        _bookings = GetRequiredService<IBookingsAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Scenario(Guid OwnerId, Guid[] ColleagueIds, Guid SpaceId);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Guests " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0, maxSeriesHorizonDays: 120));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, 8));

        async Task<Guid> NewUserAsync()
        {
            var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], $"{Guid.NewGuid():N}@test.io");
            user.SetBuildingId(building.Id);
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return user.Id;
        }

        return new Scenario(await NewUserAsync(), new[] { await NewUserAsync(), await NewUserAsync(), await NewUserAsync() }, space.Id);
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private static async Task<long> CountAsync(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<PostgresException> InsertAttendeeAsync(Guid bookingId, Guid? userId, string? email)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO "AppBookingAttendees" ("Id", "BookingId", "EndsAt", "UserId", "Email", "ResponseStatus", "IcsUid")
            VALUES (gen_random_uuid(), @booking, now(), @user, @email, 'Pending', gen_random_uuid()::text || '@dixels')
            """, connection);
        insert.Parameters.AddWithValue("booking", bookingId);
        insert.Parameters.AddWithValue("user", (object?)userId ?? DBNull.Value);
        insert.Parameters.AddWithValue("email", (object?)email ?? DBNull.Value);
        return await Should.ThrowAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
    }

    [PostgresFact]
    public async Task A_series_saves_its_guests_on_the_series_and_on_every_date()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.OwnerId);

        var created = await _bookings.CreateSeriesAsync(new CreateSeriesDto
        {
            SpaceId = s.SpaceId,
            LocalStart = Tomorrow.AddHours(10),
            LocalEnd = Tomorrow.AddHours(11),
            Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(19)) },
            IdempotencyKey = Guid.NewGuid().ToString(),
            Invitees = s.ColleagueIds.Select(id => new InviteeDto { UserId = id }).ToList(),
        });

        created.Bookings.Count.ShouldBe(20);
        (await CountAsync("""SELECT count(*) FROM "AppBookingSeriesAttendees" WHERE "SeriesId" = @id""", created.SeriesId)).ShouldBe(3);
        (await CountAsync(
                """SELECT count(*) FROM "AppBookingAttendees" a JOIN "AppBookings" b ON b."Id" = a."BookingId" WHERE b."SeriesId" = @id AND a."EndsAt" = b."EndsAt" """,
                created.SeriesId))
            .ShouldBe(60);
    }

    [PostgresFact]
    public async Task Changing_the_guests_of_a_100_date_series_is_a_few_commands_not_one_per_date()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.OwnerId);
        var created = await _bookings.CreateSeriesAsync(new CreateSeriesDto
        {
            SpaceId = s.SpaceId,
            LocalStart = Tomorrow.AddHours(10),
            LocalEnd = Tomorrow.AddHours(11),
            Recurrence = new RecurrenceDto { Frequency = RecurrenceFrequency.Daily, Interval = 1, EndDate = DateOnly.FromDateTime(Tomorrow.AddDays(99)) },
            IdempotencyKey = Guid.NewGuid().ToString(),
            Invitees = s.ColleagueIds.Take(2).Select(id => new InviteeDto { UserId = id }).ToList(),
        });
        created.Bookings.Count.ShouldBe(100);

        SqlCapture.Instance.Clear();
        var updated = await _bookings.UpdateSeriesInviteesAsync(created.SeriesId, new UpdateInviteesDto
        {
            Invitees = s.ColleagueIds.Skip(1).Select(id => new InviteeDto { UserId = id }).ToList(),
        });
        var commands = SqlCapture.Instance.Commands.Count;
        _output.WriteLine($"series guest edit: {commands} commands");

        updated.Bookings.Count.ShouldBe(100);
        updated.Bookings.ShouldAllBe(b => b.Invitees.Count == 2);
        // 100 dates × (one guest out, one in) are batched: the count doesn't grow with the dates.
        commands.ShouldBeLessThanOrEqualTo(BoundForSeriesEdit + 2 * BoundPerEmailedGuest);
    }

    [PostgresFact]
    public async Task The_database_refuses_a_guest_who_is_neither_or_both_and_a_colleague_twice()
    {
        var s = await CreateScenarioAsync();
        using var _ = ActAs(s.OwnerId);
        var booking = await _bookings.CreateAsync(new CreateBookingDto
        {
            SpaceId = s.SpaceId,
            LocalStart = Tomorrow.AddHours(10),
            LocalEnd = Tomorrow.AddHours(11),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Invitees = [new InviteeDto { UserId = s.ColleagueIds[0] }],
        });

        (await InsertAttendeeAsync(booking.Id, null, null)).SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        (await InsertAttendeeAsync(booking.Id, s.ColleagueIds[1], "x@outside.io")).SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        (await InsertAttendeeAsync(booking.Id, s.ColleagueIds[0], null)).SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }
}
