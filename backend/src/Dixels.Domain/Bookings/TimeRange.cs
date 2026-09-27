using System;

namespace Dixels.Bookings;

/// <summary>
/// A half-open UTC interval <c>[Start, End)</c> — End is exclusive, so 10:00–11:00 and
/// 11:00–12:00 touch but don't overlap. This one predicate is the overlap rule used
/// everywhere (closures, availability and booking conflicts), matching the database's
/// exclusion constraint.
/// </summary>
public readonly record struct TimeRange(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Overlaps(DateTimeOffset start, DateTimeOffset end) => Start < end && End > start;

    public bool Contains(DateTimeOffset start, DateTimeOffset end) => Start <= start && end <= End;

    public TimeSpan Duration => End - Start;
}
