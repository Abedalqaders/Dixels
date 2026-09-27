using System;
using Shouldly;
using Xunit;

namespace Dixels.Bookings.Tests;

public class BuildingClockTests
{
    // Europe/London springs forward at 01:00 on 29 Mar 2026 (01:00 → 02:00) and falls back
    // at 02:00 on 25 Oct 2026 (02:00 → 01:00).
    private readonly BuildingClock _london = new("Europe/London");

    [Fact]
    public void Converts_local_time_using_the_offset_in_force_on_that_date()
    {
        _london.ToUtc(new DateTime(2026, 1, 15, 9, 0, 0)).ShouldBe(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));
        _london.ToUtc(new DateTime(2026, 7, 15, 9, 0, 0)).ShouldBe(new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_local_time_inside_the_spring_forward_gap_moves_to_the_end_of_the_gap()
    {
        // 01:30 doesn't exist that night; the first real time after it is 02:00 BST = 01:00 UTC.
        _london.ToUtc(new DateTime(2026, 3, 29, 1, 30, 0)).ShouldBe(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void An_ambiguous_fall_back_time_resolves_to_standard_time()
    {
        // 01:30 happens twice; standard time (GMT) is the later one = 01:30 UTC.
        _london.ToUtc(new DateTime(2026, 10, 25, 1, 30, 0)).ShouldBe(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Round_trips_utc_to_local()
    {
        var utc = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

        _london.ToLocal(utc).ShouldBe(new DateTime(2026, 7, 15, 9, 0, 0));
        _london.LocalDate(utc).ShouldBe(new DateOnly(2026, 7, 15));
    }
}
