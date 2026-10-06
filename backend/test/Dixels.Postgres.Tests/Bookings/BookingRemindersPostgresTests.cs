using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Emailing;
using Dixels.EntityFrameworkCore;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace Dixels.Postgres.Bookings;

/// <summary>
/// Booking reminders with more than one app server: the run is guarded by a Postgres advisory
/// lock, so a server that doesn't hold it skips the minute and no reminder goes out twice.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BookingRemindersPostgresTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly FakeEmailSender _emails;

    public BookingRemindersPostgresTests()
    {
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _emails = GetRequiredService<FakeEmailSender>();
    }

    private sealed record Scenario(Guid UserId, string Email, Guid SpaceId);

    // Its own building, room and employee: the container's database is shared by every test.
    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "PG Reminders " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 240, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room", spaceType.Id, 8));

        var email = $"{Guid.NewGuid():N}@test.io";
        var user = new IdentityUser(Guid.NewGuid(), "pg" + Guid.NewGuid().ToString("N")[..10], email);
        user.SetBuildingId(building.Id);
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, email, space.Id);
    });

    /// <summary>
    /// <paramref name="count"/> one-minute bookings starting 10–20 minutes from now (inside the
    /// default 30-minute reminder window), made yesterday so they are due a reminder.
    /// </summary>
    private async Task<List<Guid>> AddDueBookingsAsync(Scenario s, int count)
    {
        var first = DateTimeOffset.UtcNow.AddMinutes(10);
        var ids = await WithUnitOfWorkAsync(async () =>
        {
            var created = new List<Guid>();
            for (var i = 0; i < count; i++)
            {
                var start = first.AddMinutes(i * 2);
                var booking = await _bookingRepository.InsertAsync(new Booking(
                    Guid.NewGuid(), s.SpaceId, s.UserId, start, start.AddMinutes(1),
                    attendees: 1, "Direct", "{}", Guid.NewGuid().ToString()));
                created.Add(booking.Id);
            }
            return created;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var db = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
            await db.Bookings.Where(b => ids.Contains(b.Id))
                .ExecuteUpdateAsync(u => u.SetProperty(b => b.CreationTime, DateTime.UtcNow.AddDays(-1)));
        });

        return ids;
    }

    private Task<int> CountRemindedAsync(IReadOnlyCollection<Guid> ids) =>
        WithUnitOfWorkAsync(() => _bookingRepository.CountAsync(b => ids.Contains(b.Id) && b.ReminderSentAt != null));

    private int EmailsTo(Scenario s) => _emails.Sent.Count(e => e.To == s.Email);

    [PostgresFact]
    public async Task A_server_without_the_lock_skips_the_minute()
    {
        var s = await CreateScenarioAsync();
        var ids = await AddDueBookingsAsync(s, 1);

        // Another server holds the lock: its own connection, so this is a real advisory lock.
        await using (var other = await GetRequiredService<IAbpDistributedLock>().TryAcquireAsync(BookingReminders.LockName))
        {
            other.ShouldNotBeNull();

            (await GetRequiredService<BookingReminders>().SendDueAsync()).ShouldBe(0);
            (await CountRemindedAsync(ids)).ShouldBe(0);
        }

        // Released: the next minute sends it.
        await GetRequiredService<BookingReminders>().SendDueAsync();
        (await CountRemindedAsync(ids)).ShouldBe(1);
    }

    [PostgresFact]
    public async Task Servers_running_at_the_same_moment_send_each_reminder_once()
    {
        var s = await CreateScenarioAsync();
        var ids = await AddDueBookingsAsync(s, 5);

        // Five "servers" (each its own scope, so its own connections) on the same minute.
        var runs = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            using var scope = ServiceProvider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<BookingReminders>().SendDueAsync();
        }));
        await Task.WhenAll(runs);

        (await CountRemindedAsync(ids)).ShouldBe(5);
        EmailsTo(s).ShouldBe(5);
    }
}
