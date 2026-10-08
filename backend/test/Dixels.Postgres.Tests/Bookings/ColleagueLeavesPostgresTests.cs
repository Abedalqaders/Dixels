using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
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
using Volo.Abp.Uow;
using Xunit;

namespace Dixels.Postgres.Bookings;

/// <summary>
/// A colleague leaving while the owner changes the same booking's guests: the owner's save
/// wins the race, and taking the leaver off is read again and still happens, instead of
/// failing the admin's account change.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ColleagueLeavesPostgresTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly IBookingsAppService _bookings;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public ColleagueLeavesPostgresTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Scenario(Guid SpaceId, Guid Owner, Guid Rana, Guid Omar, Guid Lina);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Leavers " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0));
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

        return new Scenario(space.Id, await NewUserAsync(), await NewUserAsync(), await NewUserAsync(), await NewUserAsync());
    });

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    /// <summary>Waits until some session in the test database is waiting for a row lock.</summary>
    private static async Task WaitForALockWaitAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var waiting = new NpgsqlCommand(
            "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND datname = current_database()", connection);

        var giveUp = DateTime.UtcNow.AddSeconds(20);
        while ((long)(await waiting.ExecuteScalarAsync())! == 0)
        {
            DateTime.UtcNow.ShouldBeLessThan(giveUp, "the leaver's save never waited on the owner's");
            await Task.Delay(50);
        }
    }

    [PostgresFact]
    public async Task An_owner_editing_guests_at_that_moment_doesnt_stop_the_leaver_coming_off()
    {
        var s = await CreateScenarioAsync();
        BookingDto booking;
        using (ActAs(s.Owner))
        {
            booking = await _bookings.CreateAsync(new CreateBookingDto
            {
                SpaceId = s.SpaceId,
                LocalStart = Tomorrow.AddHours(10),
                LocalEnd = Tomorrow.AddHours(11),
                IdempotencyKey = Guid.NewGuid().ToString(),
                Invitees = new() { new InviteeDto { UserId = s.Rana }, new InviteeDto { UserId = s.Omar } },
            });
        }

        Task leaving;
        using (var ownersEdit = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            // The owner adds Lina: saved, not yet committed, so the booking row stays locked.
            using (ActAs(s.Owner))
            {
                await _bookings.UpdateInviteesAsync(booking.Id, new UpdateInviteesDto
                {
                    Invitees = new() { new InviteeDto { UserId = s.Rana }, new InviteeDto { UserId = s.Omar }, new InviteeDto { UserId = s.Lina } },
                });
            }

            // Meanwhile Rana leaves, in her own unit of work (not this one): she reads the booking as
            // it was, and her save waits for the owner's.
            using (ExecutionContext.SuppressFlow())
            {
                leaving = Task.Run(() => WithUnitOfWorkAsync(() =>
                    GetRequiredService<BookingManager>().RemoveGuestEverywhereAsync(s.Rana)));
            }

            await WaitForALockWaitAsync();
            await ownersEdit.CompleteAsync();
        }

        // The owner's save won; Rana's was read again on top of it and went through.
        await leaving;
        var stored = await WithUnitOfWorkAsync(() => GetRequiredService<IBookingRepository>().GetAsync(booking.Id));
        stored.Invitees.Select(i => i.UserId).ShouldBe(new Guid?[] { s.Omar, s.Lina }, ignoreOrder: true);
        // The owner made it Rana, Omar and Lina (4 with the owner); Rana's leaving makes it 3.
        stored.Attendees.ShouldBe(3);
    }
}
