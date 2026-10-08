using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Settings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.SettingManagement;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// Outside guests' details are deleted a set number of days (90 by default) after the booking
/// ends or is cancelled; a series' own list once all its dates are past that. Colleagues' rows
/// and the head count stay.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class ExternalGuestCleanupTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IBookingRepository _bookingRepository;
    private readonly ExternalGuestCleanup _cleanup;

    public ExternalGuestCleanupTests()
    {
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _cleanup = GetRequiredService<ExternalGuestCleanup>();
    }

    /// <summary>A room, its owner, and Rana, a colleague who's invited alongside the outside guests.</summary>
    private sealed record Scenario(Guid RoomId, Guid Owner, Guid Rana);

    private Task<Scenario> CreateScenarioAsync() => WithUnitOfWorkAsync(async () =>
    {
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
        var room = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, capacity: 8));

        async Task<Guid> NewUserAsync(string name)
        {
            var key = Guid.NewGuid().ToString("N")[..8];
            var user = new IdentityUser(Guid.NewGuid(), "u" + key, $"{name.ToLowerInvariant()}.{key}@test.io") { Name = name };
            user.SetBuildingId(building.Id);
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
            return user.Id;
        }

        return new Scenario(room.Id, await NewUserAsync("Owner"), await NewUserAsync("Rana"));
    });

    private static List<Invitee> Outsiders(int count) =>
        Enumerable.Range(1, count).Select(i => new Invitee(null, $"guest{i}.{Guid.NewGuid():N}@outside.io", "Guest " + i)).ToList();

    /// <summary>
    /// A booking stored as it stands (past ones can't be made through the app): ending
    /// <paramref name="endedDaysAgo"/> days ago (negative = ahead), with outside guests and Rana.
    /// </summary>
    private Task<Guid> InsertAsync(Scenario s, double endedDaysAgo, int outsiders = 2, double? cancelledDaysAgo = null, Guid? seriesId = null) =>
        WithUnitOfWorkAsync(async () =>
        {
            var end = Now.AddDays(-endedDaysAgo);
            var guests = Outsiders(outsiders).Append(new Invitee(s.Rana, null, null)).ToList();
            var booking = new Booking(Guid.NewGuid(), s.RoomId, s.Owner, end.AddHours(-1), end, attendees: 1 + guests.Count + 2,
                "Direct", "{}", Guid.NewGuid().ToString(), seriesId);
            booking.SetInvitees(guests, GetRequiredService<IGuidGenerator>());
            if (cancelledDaysAgo is { } days)
            {
                booking.Cancel(s.Owner, Now.AddDays(-days), reason: null, byAdmin: false);
            }

            await _bookingRepository.InsertAsync(booking);
            return booking.Id;
        });

    /// <summary>A series with outside guests and Rana on its own list; its dates are added with <see cref="InsertAsync"/>.</summary>
    private Task<Guid> InsertSeriesAsync(Scenario s) => WithUnitOfWorkAsync(async () =>
    {
        var series = new BookingSeries(Guid.NewGuid(), s.Owner, s.RoomId, "Weekly", attendees: 6,
            DateOnly.FromDateTime(Now.AddDays(-120).UtcDateTime), new TimeOnly(10, 0), 60,
            new RecurrenceRule(RecurrenceFrequency.Weekly, 1, new[] { DayOfWeek.Monday }, MonthlyRepeat.OnDay, DateOnly.FromDateTime(Now.UtcDateTime)),
            Guid.NewGuid().ToString());
        series.SetInvitees(Outsiders(2).Append(new Invitee(s.Rana, null, null)).ToList(), GetRequiredService<IGuidGenerator>());
        await GetRequiredService<IRepository<BookingSeries, Guid>>().InsertAsync(series);
        return series.Id;
    });

    private Task<DixelsDbContext> DbContextAsync() =>
        GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();

    /// <summary>Who's left on the booking: "outside" for each outside guest, "Rana" for her.</summary>
    private Task<List<string>> GuestsOfAsync(Scenario s, Guid bookingId) => WithUnitOfWorkAsync(async () =>
        (await (await DbContextAsync()).Set<BookingAttendee>().Where(a => a.BookingId == bookingId).ToListAsync())
        .Select(a => a.UserId == s.Rana ? "Rana" : "outside").Order(StringComparer.Ordinal).ToList());

    private Task<List<string>> SeriesGuestsAsync(Scenario s, Guid seriesId) => WithUnitOfWorkAsync(async () =>
        (await (await DbContextAsync()).Set<BookingSeriesAttendee>().Where(a => a.SeriesId == seriesId).ToListAsync())
        .Select(a => a.UserId == s.Rana ? "Rana" : "outside").Order(StringComparer.Ordinal).ToList());

    private Task SetRetentionAsync(string? days) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ISettingManager>().SetGlobalAsync(DixelsSettings.ExternalGuestRetentionDays, days));

    [Fact]
    public async Task Outside_guests_go_90_days_after_the_end_and_colleagues_stay()
    {
        var s = await CreateScenarioAsync();
        var old = await InsertAsync(s, endedDaysAgo: 91);
        var recent = await InsertAsync(s, endedDaysAgo: 89);

        (await _cleanup.RunAsync()).ShouldBeGreaterThanOrEqualTo(2);

        (await GuestsOfAsync(s, old)).ShouldBe(new[] { "Rana" });
        (await GuestsOfAsync(s, recent)).ShouldBe(new[] { "Rana", "outside", "outside" });

        // The head count still reads as it did.
        (await WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(old))).Attendees.ShouldBe(6);
    }

    [Fact]
    public async Task A_cancelled_booking_counts_from_when_it_was_cancelled()
    {
        var s = await CreateScenarioAsync();
        // Cancelled long ago, though it would only have taken place next week.
        var cancelledLongAgo = await InsertAsync(s, endedDaysAgo: -7, cancelledDaysAgo: 91);
        var cancelledLately = await InsertAsync(s, endedDaysAgo: -7, cancelledDaysAgo: 10);

        await _cleanup.RunAsync();

        (await GuestsOfAsync(s, cancelledLongAgo)).ShouldBe(new[] { "Rana" });
        (await GuestsOfAsync(s, cancelledLately)).ShouldBe(new[] { "Rana", "outside", "outside" });
    }

    [Fact]
    public async Task A_series_list_goes_only_once_every_date_is_past_the_window()
    {
        var s = await CreateScenarioAsync();

        var allOld = await InsertSeriesAsync(s);
        await InsertAsync(s, endedDaysAgo: 105, seriesId: allOld);
        // A later date, cancelled long ago, doesn't hold the list either.
        await InsertAsync(s, endedDaysAgo: 30, cancelledDaysAgo: 95, seriesId: allOld);

        var stillRunning = await InsertSeriesAsync(s);
        var oldDate = await InsertAsync(s, endedDaysAgo: 105, seriesId: stillRunning);
        await InsertAsync(s, endedDaysAgo: 20, seriesId: stillRunning);

        await _cleanup.RunAsync();

        (await SeriesGuestsAsync(s, allOld)).ShouldBe(new[] { "Rana" });
        (await SeriesGuestsAsync(s, stillRunning)).ShouldBe(new[] { "Rana", "outside", "outside" });
        // Its old date's own copy is past the window like any booking.
        (await GuestsOfAsync(s, oldDate)).ShouldBe(new[] { "Rana" });
    }

    [Fact]
    public async Task The_setting_moves_the_cutoff_and_zero_turns_it_off()
    {
        var s = await CreateScenarioAsync();
        var fortyDaysAgo = await InsertAsync(s, endedDaysAgo: 40);
        var yearAgo = await InsertAsync(s, endedDaysAgo: 365);

        try
        {
            await SetRetentionAsync("0");
            (await _cleanup.RunAsync()).ShouldBe(0);
            (await GuestsOfAsync(s, yearAgo)).ShouldBe(new[] { "Rana", "outside", "outside" });

            await SetRetentionAsync("30");
            await _cleanup.RunAsync();
            (await GuestsOfAsync(s, fortyDaysAgo)).ShouldBe(new[] { "Rana" });
            (await GuestsOfAsync(s, yearAgo)).ShouldBe(new[] { "Rana" });
        }
        finally
        {
            await SetRetentionAsync(null);
        }
    }

    [Fact]
    public async Task Many_rows_go_in_batches()
    {
        var s = await CreateScenarioAsync();
        // Two and a half batches.
        var perBooking = ExternalGuestCleanup.BatchSize / 2;
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await InsertAsync(s, endedDaysAgo: 200 + i, outsiders: perBooking));
        }

        (await _cleanup.RunAsync()).ShouldBeGreaterThanOrEqualTo(5 * perBooking);

        foreach (var id in ids)
        {
            (await GuestsOfAsync(s, id)).ShouldBe(new[] { "Rana" });
        }
    }
}
