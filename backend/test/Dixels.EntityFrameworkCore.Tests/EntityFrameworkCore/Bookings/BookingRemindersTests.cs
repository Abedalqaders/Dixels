using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>Which bookings the reminder job picks up, batch by batch (see BookingReminders).</summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingRemindersTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingRepository _bookingRepository;

    public BookingRemindersTests()
    {
        _bookingRepository = GetRequiredService<IBookingRepository>();
    }

    private sealed record Scenario(Guid UserId, Guid BuildingId, Guid FloorId, Guid SpaceId);

    /// <summary>A UTC, 24/7 building with one room and an employee in it.</summary>
    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "Reminders " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, 8));

        var user = new IdentityUser(Guid.NewGuid(), "rem" + Guid.NewGuid().ToString("N")[..8], $"{Guid.NewGuid():N}@test.io");
        user.SetBuildingId(building.Id);
        (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

        return new Scenario(user.Id, building.Id, floor.Id, space.Id);
    });

    /// <summary>
    /// <paramref name="count"/> bookings starting 10 minutes from now (SQLite has no overlap
    /// constraint, so they can share the slot). Made yesterday unless <paramref name="madeNow"/>,
    /// i.e. booked inside the reminder window.
    /// </summary>
    private async Task<List<Guid>> AddSoonBookingsAsync(Scenario s, int count, bool madeNow = false)
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(10);
        var ids = await WithUnitOfWorkAsync(async () =>
        {
            var created = new List<Guid>();
            for (var i = 0; i < count; i++)
            {
                var booking = await _bookingRepository.InsertAsync(new Booking(
                    Guid.NewGuid(), s.SpaceId, s.UserId, start, start.AddMinutes(30),
                    attendees: 1, "Direct", "{}", Guid.NewGuid().ToString()));
                created.Add(booking.Id);
            }
            return created;
        });

        if (!madeNow)
        {
            await WithDbContextAsync(db => db.Bookings.Where(b => ids.Contains(b.Id))
                .ExecuteUpdateAsync(u => u.SetProperty(b => b.CreationTime, DateTime.UtcNow.AddDays(-1))));
        }

        return ids;
    }

    private Task WithDbContextAsync(Func<DixelsDbContext, Task> action) => WithUnitOfWorkAsync(async () =>
        await action(await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync()));

    private Task<int> CountRemindedAsync(IReadOnlyCollection<Guid> ids) =>
        WithUnitOfWorkAsync(() => _bookingRepository.CountAsync(b => ids.Contains(b.Id) && b.ReminderSentAt != null));

    private Task<int> SendDueRemindersAsync() => GetRequiredService<BookingReminders>().SendDueAsync();

    [Fact]
    public async Task More_than_one_batch_is_sent_in_one_run()
    {
        var s = await CreateScenarioAsync();
        var ids = await AddSoonBookingsAsync(s, BookingReminders.BatchSize * 2 + 50);

        await SendDueRemindersAsync();

        (await CountRemindedAsync(ids)).ShouldBe(ids.Count);
    }

    [Fact]
    public async Task Bookings_made_inside_the_window_dont_hold_up_the_rest()
    {
        var s = await CreateScenarioAsync();
        // A full batch and more that get no reminder, ahead of one that does.
        var madeNow = await AddSoonBookingsAsync(s, BookingReminders.BatchSize + 10, madeNow: true);
        var due = await AddSoonBookingsAsync(s, 1);

        await SendDueRemindersAsync();

        (await CountRemindedAsync(due)).ShouldBe(1);
        (await CountRemindedAsync(madeNow)).ShouldBe(0);
    }

    [Theory]
    [InlineData("room")]
    [InlineData("floor")]
    [InlineData("building")]
    public async Task A_booking_in_a_removed_room_gets_no_reminder(string removed)
    {
        var s = await CreateScenarioAsync();
        var ids = await AddSoonBookingsAsync(s, 1);

        // Removed, but its bookings not cancelled yet (that's a background job). Straight in the
        // database, so no delete handler cancels the booking first.
        await WithDbContextAsync(db => removed switch
        {
            "room" => db.Spaces.Where(x => x.Id == s.SpaceId).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsDeleted, true)),
            "floor" => db.Floors.Where(x => x.Id == s.FloorId).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsDeleted, true)),
            _ => db.Buildings.Where(x => x.Id == s.BuildingId).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsDeleted, true)),
        });

        await SendDueRemindersAsync();

        (await CountRemindedAsync(ids)).ShouldBe(0);
    }
}
