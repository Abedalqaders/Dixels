using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Dixels.Bookings;

/// <summary>
/// Turns a <see cref="RecurrenceRule"/> into the calendar dates it lands on, from the first
/// date up to its end date. Pure date arithmetic on building-local <see cref="DateOnly"/>s:
/// the time of day is applied per date afterwards (and converted to UTC per date), so a
/// clock change between two dates never shifts the booking's wall-clock time.
/// </summary>
public static class RecurrenceExpander
{
    public static IReadOnlyList<DateOnly> Expand(RecurrenceRule rule, DateOnly first, int maxOccurrences)
    {
        if (rule.EndDate < first)
        {
            throw new BusinessException(DixelsDomainErrorCodes.SeriesEndBeforeStart);
        }

        var dates = new List<DateOnly>();

        void Add(DateOnly date)
        {
            dates.Add(date);
            if (dates.Count > maxOccurrences)
            {
                throw new BusinessException(DixelsDomainErrorCodes.SeriesTooManyOccurrences).WithData("max", maxOccurrences);
            }
        }

        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                for (var d = first; d <= rule.EndDate; d = d.AddDays(rule.Interval))
                {
                    Add(d);
                }

                break;

            case RecurrenceFrequency.Weekly:
                // Weeks run Sunday to Saturday; "every 2 weeks" counts from the first date's week.
                for (var week = first.AddDays(-(int)first.DayOfWeek); week <= rule.EndDate; week = week.AddDays(7 * rule.Interval))
                {
                    foreach (var day in rule.Weekdays)
                    {
                        var d = week.AddDays((int)day);
                        if (d >= first && d <= rule.EndDate)
                        {
                            Add(d);
                        }
                    }
                }

                break;

            case RecurrenceFrequency.Monthly:
                var firstOfMonth = new DateOnly(first.Year, first.Month, 1);
                var position = WeekOfMonth(first);
                for (var i = 0; ; i++)
                {
                    var month = firstOfMonth.AddMonths(i * rule.Interval);
                    if (month > rule.EndDate)
                    {
                        break;
                    }

                    var d = rule.MonthlyRepeat == MonthlyRepeat.OnDay
                        ? new DateOnly(month.Year, month.Month, Math.Min(first.Day, DateTime.DaysInMonth(month.Year, month.Month)))
                        : NthWeekday(month, first.DayOfWeek, position);

                    if (d >= first && d <= rule.EndDate)
                    {
                        Add(d);
                    }
                }

                break;
        }

        return dates;
    }

    /// <summary>1–4 for "the 1st…4th Tuesday"; 5 for a date in the 5th week, which repeats as "the last Tuesday".</summary>
    public static int WeekOfMonth(DateOnly date) => (date.Day - 1) / 7 + 1;

    private static DateOnly NthWeekday(DateOnly firstOfMonth, DayOfWeek day, int position)
    {
        var first = firstOfMonth.AddDays(((int)day - (int)firstOfMonth.DayOfWeek + 7) % 7);
        if (position <= 4)
        {
            return first.AddDays(7 * (position - 1));
        }

        // "The last": the 5th if the month has one, else the 4th.
        var fifth = first.AddDays(28);
        return fifth.Month == firstOfMonth.Month ? fifth : first.AddDays(21);
    }
}
