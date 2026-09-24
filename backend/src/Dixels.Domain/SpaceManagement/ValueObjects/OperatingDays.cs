using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace Dixels.SpaceManagement.ValueObjects;

/// <summary>
/// A set of weekdays, stored as a 7-bit mask. Bit <c>N</c> corresponds exactly to
/// <see cref="DayOfWeek"/>'s own numbering (Sunday = 0 ... Saturday = 6), so this class
/// deliberately reuses the BCL's convention instead of inventing its own — that is what
/// pins "which bit is Sunday" for every caller, in both the backend and the frontend.
/// </summary>
public sealed class OperatingDays : ValueObject
{
    public const int AllDaysMask = 0b111_1111;

    public int Mask { get; }

    public static readonly OperatingDays Everyday = new(AllDaysMask);
    public static readonly OperatingDays None = new(0);

    public OperatingDays(int mask)
    {
        if (mask < 0 || mask > AllDaysMask)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mask),
                mask,
                "Operating days mask must be between 0 and 127 (7 bits, Sunday..Saturday).");
        }

        Mask = mask;
    }

    public static OperatingDays FromDayOfWeeks(IEnumerable<DayOfWeek> days)
    {
        if (days is null)
        {
            throw new ArgumentNullException(nameof(days));
        }

        var mask = 0;
        foreach (var day in days)
        {
            mask |= 1 << (int)day;
        }

        return new OperatingDays(mask);
    }

    public bool Contains(DayOfWeek day) => (Mask & (1 << (int)day)) != 0;

    /// <summary>
    /// True if every day this window covers is also covered by <paramref name="parent"/> —
    /// the narrow-only rule: a child can restrict which days it's reachable further than its
    /// parent, never grant a day the parent doesn't have.
    /// </summary>
    public bool IsSubsetOf(OperatingDays parent)
    {
        if (parent is null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        return (Mask & ~parent.Mask) == 0;
    }

    public IEnumerable<DayOfWeek> ToDayOfWeeks()
    {
        for (var i = 0; i < 7; i++)
        {
            if ((Mask & (1 << i)) != 0)
            {
                yield return (DayOfWeek)i;
            }
        }
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Mask;
    }
}
