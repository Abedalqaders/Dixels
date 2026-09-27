using System;
using Shouldly;
using Xunit;

namespace Dixels.Bookings.Tests;

public class FreeTimeTests
{
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 9, 29, hour, minute, 0, TimeSpan.Zero);

    private static readonly TimeRange[] OpenNineToFive = { new(At(9), At(17)) };

    [Fact]
    public void Free_until_stops_at_the_next_booking()
    {
        var blockers = new[] { new TimeRange(At(14), At(15)) };

        FreeTime.FreeUntil(OpenNineToFive, blockers, At(10), At(11)).ShouldBe(At(14));
    }

    [Fact]
    public void Free_until_is_closing_time_when_nothing_follows()
    {
        var earlier = new[] { new TimeRange(At(9), At(10)) };

        FreeTime.FreeUntil(OpenNineToFive, earlier, At(10), At(11)).ShouldBe(At(17));
    }

    [Fact]
    public void A_booking_starting_exactly_at_the_end_limits_the_free_stretch_to_the_end()
    {
        var backToBack = new[] { new TimeRange(At(11), At(12)) };

        FreeTime.FreeUntil(OpenNineToFive, backToBack, At(10), At(11)).ShouldBe(At(11));
    }

    [Fact]
    public void Next_free_start_skips_past_the_blocking_booking_on_the_slot_grid()
    {
        var blockers = new[] { new TimeRange(At(10), At(11, 30)) };

        FreeTime.NextFreeStart(OpenNineToFive, blockers, At(10), TimeSpan.FromHours(1), 15, At(23, 59))
            .ShouldBe(At(11, 30));
    }

    [Fact]
    public void Next_free_start_needs_the_whole_length_to_fit_before_the_next_blocker()
    {
        // 11:00–11:30 is free but too short for an hour; the next hour-long gap starts 12:00.
        var blockers = new[] { new TimeRange(At(10), At(11)), new TimeRange(At(11, 30), At(12)) };

        FreeTime.NextFreeStart(OpenNineToFive, blockers, At(10), TimeSpan.FromHours(1), 15, At(23))
            .ShouldBe(At(12));
    }

    [Fact]
    public void Next_free_start_is_null_when_nothing_fits_before_closing()
    {
        var blockers = new[] { new TimeRange(At(10), At(17)) };

        FreeTime.NextFreeStart(OpenNineToFive, blockers, At(10), TimeSpan.FromHours(1), 15, At(23)).ShouldBeNull();
    }
}
