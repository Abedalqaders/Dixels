using System;
using Shouldly;
using Xunit;

namespace Dixels.Bookings.Tests;

public class TimeRangeTests
{
    private static DateTimeOffset At(int hour) => new(2026, 9, 29, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Back_to_back_ranges_do_not_overlap_because_the_end_is_exclusive()
    {
        new TimeRange(At(10), At(11)).Overlaps(At(11), At(12)).ShouldBeFalse();
        new TimeRange(At(11), At(12)).Overlaps(At(10), At(11)).ShouldBeFalse();
    }

    [Fact]
    public void Any_shared_minute_is_an_overlap()
    {
        new TimeRange(At(10), At(12)).Overlaps(At(11), At(13)).ShouldBeTrue();
        new TimeRange(At(10), At(13)).Overlaps(At(11), At(12)).ShouldBeTrue();
    }
}
