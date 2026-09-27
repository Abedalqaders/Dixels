using System;
using System.Collections.Generic;
using Dixels.SpaceManagement;

namespace Dixels.Bookings;

/// <summary>The outcome of checking one request: where, when (UTC), under which rules, and what failed.</summary>
public sealed record BookingEvaluation(
    Space Space,
    Floor Floor,
    Building Building,
    BuildingClock LocalClock,
    ResolvedConstraints Rules,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    IReadOnlyList<BookingViolation> Violations)
{
    public bool IsValid => Violations.Count == 0;
}
