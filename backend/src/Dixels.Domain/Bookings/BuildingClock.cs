using System;

namespace Dixels.Bookings;

/// <summary>
/// Converts between UTC instants and a building's local wall-clock time. Every booking
/// rule about days and hours is a wall-clock rule ("open 07:00–20:00 in Amman"), so it is
/// evaluated in building-local time, while everything stored and compared for overlap is
/// UTC. A fixed UTC offset is never stored — it would be wrong on the other side of a DST
/// change.
/// </summary>
public sealed class BuildingClock
{
    private static readonly TimeSpan GapProbeStep = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxGap = TimeSpan.FromHours(3);

    public TimeZoneInfo Zone { get; }

    public BuildingClock(string ianaTimezone)
    {
        // Building.SetTimezone already validated this id against the same lookup.
        Zone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone);
    }

    public DateTime ToLocal(DateTimeOffset utc)
    {
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(utc, Zone).DateTime, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Local wall-clock time → UTC. Two DST edge cases, both handled deterministically:
    /// a local time inside a spring-forward gap (e.g. 00:30 on a night that jumps from 00:00
    /// to 01:00) doesn't exist, so it moves forward to the first valid time after the gap;
    /// an ambiguous local time in a fall-back hour resolves to the standard-time offset
    /// (<see cref="TimeZoneInfo.GetUtcOffset(DateTime)"/>'s own rule).
    /// </summary>
    public DateTimeOffset ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        var probed = TimeSpan.Zero;
        while (Zone.IsInvalidTime(local) && probed < MaxGap)
        {
            local = local.Add(GapProbeStep);
            probed += GapProbeStep;
        }

        var offset = Zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    /// <summary>Midnight at the start of <paramref name="date"/> in building-local time, as UTC.</summary>
    public DateTimeOffset StartOfLocalDay(DateOnly date)
    {
        return ToUtc(date.ToDateTime(TimeOnly.MinValue));
    }

    public DateOnly LocalDate(DateTimeOffset utc)
    {
        return DateOnly.FromDateTime(ToLocal(utc));
    }
}
