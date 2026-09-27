using System;
using System.Collections.Generic;
using Dixels.SpaceManagement;

namespace Dixels.Bookings;

/// <summary>A confirmed booking's time on the day timeline — who booked it is never exposed, only whether it's yours.</summary>
public sealed record BusyRange(TimeRange Range, bool IsMine);

/// <summary>
/// One space's answer to an availability search: whether the requested window is bookable
/// (from the very same validator a real booking runs), and the day around it — open times,
/// closures and existing bookings — plus the "free until" / "next free" hints.
/// </summary>
public sealed record SpaceAvailability(
    Space Space,
    Floor Floor,
    ResolvedConstraints Rules,
    IReadOnlyList<BookingViolation> Violations,
    IReadOnlyList<TimeRange> Open,
    IReadOnlyList<TimeRange> Closed,
    IReadOnlyList<BusyRange> Busy,
    DateTimeOffset? FreeUntil,
    DateTimeOffset? NextFreeStart)
{
    public bool IsAvailable => Violations.Count == 0;
}

/// <summary>The whole search: the day it covers (building-local midnight to midnight, as UTC) and every space's answer.</summary>
public sealed record AvailabilitySearch(
    Building Building,
    BuildingClock LocalClock,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    TimeRange Day,
    IReadOnlyList<SpaceAvailability> Spaces);
