using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.Bookings;

/// <summary>
/// Turns resolved operating days + hours (wall-clock rules) into the concrete UTC intervals
/// a space is open over a date range, adding any special openings (<c>Open</c> overrides)
/// and merging intervals that touch. Closures are deliberately NOT subtracted here — a
/// closure is its own, more fundamental rejection ("closed for maintenance", not "outside
/// hours"), so it's checked separately.
///
/// Merging is what makes "a booking must fit inside one open interval" behave correctly:
/// a 24/7 building's Monday and Tuesday windows merge into one, so 23:00–01:00 is fine,
/// while a Sun–Thu 07:00–20:00 floor never merges across the night, so it isn't.
/// </summary>
public static class OpenIntervals
{
    public static IReadOnlyList<TimeRange> Compute(
        OperatingDays days,
        OperatingWindow hours,
        BuildingClock clock,
        DateOnly fromLocalDate,
        DateOnly toLocalDate,
        IEnumerable<TimeRange>? specialOpenings = null)
    {
        var ranges = new List<TimeRange>();

        // Start one day early: a window that wraps past midnight (22:00–02:00) opened the
        // previous evening still covers the early hours of fromLocalDate.
        for (var date = fromLocalDate.AddDays(-1); date <= toLocalDate; date = date.AddDays(1))
        {
            if (!days.Contains(date.DayOfWeek))
            {
                continue;
            }

            var (localStart, localEnd) = WindowOn(date, hours);
            ranges.Add(new TimeRange(clock.ToUtc(localStart), clock.ToUtc(localEnd)));
        }

        if (specialOpenings is not null)
        {
            ranges.AddRange(specialOpenings);
        }

        return Merge(ranges);
    }

    /// <summary>
    /// The first instant in <c>[start, end)</c> that no interval covers, or null when the
    /// whole request sits inside a single interval. Used to explain a rejection ("you run
    /// past closing time" vs "that day is closed") by looking at where coverage stops.
    /// </summary>
    public static DateTimeOffset? FirstUncovered(IReadOnlyList<TimeRange> merged, DateTimeOffset start, DateTimeOffset end)
    {
        var cursor = start;
        foreach (var range in merged)
        {
            if (range.Start <= cursor && cursor < range.End)
            {
                cursor = range.End;
            }
        }

        return cursor < end ? cursor : null;
    }

    private static (DateTime Start, DateTime End) WindowOn(DateOnly date, OperatingWindow hours)
    {
        if (hours.IsOpen24Hours)
        {
            return (date.ToDateTime(TimeOnly.MinValue), date.AddDays(1).ToDateTime(TimeOnly.MinValue));
        }

        var start = date.ToDateTime(hours.Open);

        // Close at or before Open means the window wraps past midnight — Close is on the
        // next day. (Close == Open is only legal for a 24h window, handled above.)
        var end = hours.Close > hours.Open
            ? date.ToDateTime(hours.Close)
            : date.AddDays(1).ToDateTime(hours.Close);

        return (start, end);
    }

    private static IReadOnlyList<TimeRange> Merge(List<TimeRange> ranges)
    {
        var merged = new List<TimeRange>();
        foreach (var range in ranges.Where(r => r.End > r.Start).OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = new TimeRange(last.Start, range.End > last.End ? range.End : last.End);
            }
            else
            {
                merged.Add(range);
            }
        }

        return merged;
    }
}
