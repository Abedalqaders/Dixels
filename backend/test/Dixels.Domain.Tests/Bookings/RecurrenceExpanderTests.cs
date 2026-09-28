using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dixels.Bookings.Tests;

public class RecurrenceExpanderTests
{
    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    private static DateOnly[] Expand(RecurrenceRule rule, DateOnly first, int max = 100) =>
        RecurrenceExpander.Expand(rule, first, max).ToArray();

    private static RecurrenceRule Rule(
        RecurrenceFrequency frequency, int interval, DateOnly end, MonthlyRepeat monthly = MonthlyRepeat.OnDay, params DayOfWeek[] days) =>
        new(frequency, interval, days, monthly, end);

    [Fact]
    public void Daily_every_other_day_up_to_and_including_the_end()
    {
        // Tue 29 Sep 2026, every 2 days.
        Expand(Rule(RecurrenceFrequency.Daily, 2, D(10, 5)), D(9, 29))
            .ShouldBe(new[] { D(9, 29), D(10, 1), D(10, 3), D(10, 5) });
    }

    [Fact]
    public void Weekly_on_the_chosen_days()
    {
        Expand(Rule(RecurrenceFrequency.Weekly, 1, D(10, 8), days: new[] { DayOfWeek.Thursday, DayOfWeek.Tuesday }), D(9, 29))
            .ShouldBe(new[] { D(9, 29), D(10, 1), D(10, 6), D(10, 8) });
    }

    [Fact]
    public void Weekly_skips_chosen_days_before_the_first_date()
    {
        // Starts Wed 30 Sep: that week's Tuesday has already gone.
        Expand(Rule(RecurrenceFrequency.Weekly, 1, D(10, 8), days: new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday }), D(9, 30))
            .ShouldBe(new[] { D(10, 1), D(10, 6), D(10, 8) });
    }

    [Fact]
    public void Every_two_weeks_counts_from_the_first_week()
    {
        Expand(Rule(RecurrenceFrequency.Weekly, 2, D(10, 27), days: new[] { DayOfWeek.Tuesday }), D(9, 29))
            .ShouldBe(new[] { D(9, 29), D(10, 13), D(10, 27) });
    }

    [Fact]
    public void Monthly_on_the_31st_uses_the_last_day_of_shorter_months()
    {
        Expand(Rule(RecurrenceFrequency.Monthly, 1, D(5, 31)), D(1, 31))
            .ShouldBe(new[] { D(1, 31), D(2, 28), D(3, 31), D(4, 30), D(5, 31) });
    }

    [Fact]
    public void Monthly_on_the_same_weekday_position()
    {
        // Tue 13 Oct 2026 is the 2nd Tuesday → 2nd Tuesday of Nov (10th) and Dec (8th).
        Expand(Rule(RecurrenceFrequency.Monthly, 1, D(12, 31), MonthlyRepeat.OnWeekday), D(10, 13))
            .ShouldBe(new[] { D(10, 13), D(11, 10), D(12, 8) });
    }

    [Fact]
    public void Monthly_from_a_fifth_week_date_repeats_on_the_last_one()
    {
        // Thu 29 Oct 2026 is the 5th Thursday → the last Thursday: 26 Nov (4th), 31 Dec (5th).
        Expand(Rule(RecurrenceFrequency.Monthly, 1, D(12, 31), MonthlyRepeat.OnWeekday), D(10, 29))
            .ShouldBe(new[] { D(10, 29), D(11, 26), D(12, 31) });
    }

    [Fact]
    public void Every_two_months()
    {
        Expand(Rule(RecurrenceFrequency.Monthly, 2, D(3, 31, 2027)), D(9, 15))
            .ShouldBe(new[] { D(9, 15), D(11, 15), D(1, 15, 2027), D(3, 15, 2027) });
    }

    [Fact]
    public void An_end_before_the_first_date_is_rejected()
    {
        Should.Throw<BusinessException>(() => Expand(Rule(RecurrenceFrequency.Daily, 1, D(9, 28)), D(9, 29)))
            .Code.ShouldBe(DixelsDomainErrorCodes.SeriesEndBeforeStart);
    }

    [Fact]
    public void More_than_the_maximum_is_rejected()
    {
        var ex = Should.Throw<BusinessException>(() => Expand(Rule(RecurrenceFrequency.Daily, 1, D(12, 31)), D(1, 1), max: 100));
        ex.Code.ShouldBe(DixelsDomainErrorCodes.SeriesTooManyOccurrences);
        ex.Data["max"].ShouldBe(100);
    }

    [Fact]
    public void Weekly_with_no_day_picked_is_a_mistake_not_a_guess()
    {
        Should.Throw<BusinessException>(() => Rule(RecurrenceFrequency.Weekly, 1, D(10, 31)))
            .Code.ShouldBe(DixelsDomainErrorCodes.SeriesNoWeekdays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void The_interval_must_be_1_to_99(int interval)
    {
        Should.Throw<BusinessException>(() => Rule(RecurrenceFrequency.Daily, interval, D(10, 31)))
            .Code.ShouldBe(DixelsDomainErrorCodes.SeriesInvalidInterval);
    }

    [Fact]
    public void Weekdays_round_trip_through_the_stored_mask()
    {
        var rule = Rule(RecurrenceFrequency.Weekly, 1, D(10, 31), days: new[] { DayOfWeek.Sunday, DayOfWeek.Thursday });
        RecurrenceRule.WeekdaysFromMask(rule.WeekdaysMask).ShouldBe(new[] { DayOfWeek.Sunday, DayOfWeek.Thursday });
    }
}
