using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace Dixels.SpaceManagement.ValueObjects;

/// <summary>
/// An operating-hours window on a 24-hour clock. <see cref="IsOpen24Hours"/> is an explicit
/// flag rather than being inferred from <c>Open == Close</c> — that inference was ambiguous
/// (indistinguishable from a lazy default or a data-entry mistake) and didn't read as an
/// intentional choice, so it was replaced with this flag after review. When
/// <see cref="IsOpen24Hours"/> is false and <see cref="Close"/> is earlier than
/// <see cref="Open"/>, the window wraps past midnight (e.g. 22:00-02:00) — a real, supported
/// case, since overnight operation (reception desks, night-shift studios) is a predictable
/// product need.
/// </summary>
public sealed class OperatingWindow : ValueObject
{
    private const int MinutesPerDay = 24 * 60;

    public bool IsOpen24Hours { get; }
    public TimeOnly Open { get; }
    public TimeOnly Close { get; }

    public static readonly OperatingWindow FullDay = new(isOpen24Hours: true, TimeOnly.MinValue, TimeOnly.MinValue);

    /// <summary>
    /// Convenience factory for a non-24-hour window — equivalent to
    /// <c>new OperatingWindow(false, open, close)</c>. Not a second constructor: EF Core's
    /// constructor-based materialization picked a 2-arg constructor over this one even when
    /// both were public (its tie-break isn't "most parameters wins" the way the docs read),
    /// silently losing <see cref="IsOpen24Hours"/> and throwing on a stored full-24h row
    /// where Open == Close. Keeping exactly one constructor removes the ambiguity outright
    /// regardless of EF's exact selection rule.
    /// </summary>
    public static OperatingWindow Create(TimeOnly open, TimeOnly close)
    {
        return new OperatingWindow(isOpen24Hours: false, open, close);
    }

    public OperatingWindow(bool isOpen24Hours, TimeOnly open, TimeOnly close)
    {
        if (!isOpen24Hours && open == close)
        {
            throw new ArgumentException(
                "Open and close can't be equal for a non-24-hour window — use OperatingWindow.FullDay to express 24 hours explicitly.",
                nameof(close));
        }

        IsOpen24Hours = isOpen24Hours;
        Open = isOpen24Hours ? TimeOnly.MinValue : open;
        Close = isOpen24Hours ? TimeOnly.MinValue : close;
    }

    /// <summary>
    /// True if every instant this window covers is also covered by <paramref name="parent"/>.
    /// Verified against every combination of wrapping/non-wrapping and full-24h windows: a
    /// non-wrapping child inside a non-wrapping parent, a wrapping child inside a wrapping
    /// parent, a non-wrapping child inside a wrapping parent (both segments), a wrapping
    /// child inside a non-wrapping parent (always false), a full-24h parent (always true),
    /// a full-24h child against a non-full parent (always false), and exact-boundary
    /// equality (true). See shared/test-fixtures/operating-window-cases.json for the
    /// canonical case table this implementation is tested against.
    /// </summary>
    public bool IsSubsetOf(OperatingWindow parent)
    {
        if (parent is null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        if (parent.IsOpen24Hours)
        {
            return true;
        }

        if (IsOpen24Hours)
        {
            return false;
        }

        // Rotate both windows so parent.Open maps to minute 0 — this turns containment
        // into a plain forward-sweep comparison regardless of whether either window
        // wraps past midnight in absolute clock time.
        var parentOpenMinutes = ToMinutes(parent.Open);
        var parentLength = Mod(ToMinutes(parent.Close) - parentOpenMinutes);
        var childStart = Mod(ToMinutes(Open) - parentOpenMinutes);
        var childEnd = Mod(ToMinutes(Close) - parentOpenMinutes);

        if (childStart > childEnd)
        {
            // The child's rotated sweep itself wraps, so it necessarily exits the
            // parent's arc (the parent-is-full-24h case was already handled above).
            return false;
        }

        return childEnd <= parentLength;
    }

    private static int ToMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;

    private static int Mod(int value) => ((value % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return IsOpen24Hours;
        yield return Open;
        yield return Close;
    }
}
