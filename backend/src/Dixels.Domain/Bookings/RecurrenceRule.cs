using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Dixels.Bookings;

/// <summary>
/// How a recurring booking repeats, Teams-style: every <see cref="Interval"/> days, weeks
/// (on the chosen <see cref="Weekdays"/>) or months (on the same date, or the same weekday
/// position), up to and including <see cref="EndDate"/>. Always ends — a series has to be
/// checkable date by date against the building's series horizon.
/// </summary>
public sealed class RecurrenceRule
{
    public const int MaxInterval = 99;

    public RecurrenceFrequency Frequency { get; }
    public int Interval { get; }

    /// <summary>Weekly only: the days it repeats on, Sunday first. Empty for daily/monthly.</summary>
    public IReadOnlyList<DayOfWeek> Weekdays { get; }

    public MonthlyRepeat MonthlyRepeat { get; }

    /// <summary>The last date an occurrence may fall on (inclusive), in building-local time.</summary>
    public DateOnly EndDate { get; }

    public RecurrenceRule(
        RecurrenceFrequency frequency,
        int interval,
        IEnumerable<DayOfWeek>? weekdays,
        MonthlyRepeat monthlyRepeat,
        DateOnly endDate)
    {
        if (!Enum.IsDefined(frequency) || !Enum.IsDefined(monthlyRepeat))
        {
            throw new BusinessException(DixelsDomainErrorCodes.SeriesInvalidRule);
        }

        if (interval < 1 || interval > MaxInterval)
        {
            throw new BusinessException(DixelsDomainErrorCodes.SeriesInvalidInterval).WithData("max", MaxInterval);
        }

        var days = (weekdays ?? Array.Empty<DayOfWeek>()).Where(Enum.IsDefined).Distinct().OrderBy(d => (int)d).ToList();

        // Weekly with no day picked is a mistake to point out, not something to guess
        // ("the start date's weekday") behind the person's back.
        if (frequency == RecurrenceFrequency.Weekly && days.Count == 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.SeriesNoWeekdays);
        }

        Frequency = frequency;
        Interval = interval;
        Weekdays = frequency == RecurrenceFrequency.Weekly ? days : Array.Empty<DayOfWeek>();
        MonthlyRepeat = monthlyRepeat;
        EndDate = endDate;
    }

    /// <summary>The weekdays as a Sunday = bit 0 … Saturday = bit 6 mask, for storage.</summary>
    public int WeekdaysMask => Weekdays.Aggregate(0, (mask, d) => mask | (1 << (int)d));

    public static IEnumerable<DayOfWeek> WeekdaysFromMask(int mask) =>
        Enumerable.Range(0, 7).Where(i => (mask & (1 << i)) != 0).Select(i => (DayOfWeek)i);
}
