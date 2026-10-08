using System;
using System.Collections.Generic;
using Dixels.SpaceManagement;

namespace Dixels.Bookings;

/// <summary>
/// The room a booking was made in, with its floor and building, as the create loaded them
/// (names included), so whoever shows the result needn't load them again.
/// </summary>
public sealed record BookingPlace(Space Space, Floor Floor, Building Building);

/// <summary>
/// The outcome of checking one request: where, when (UTC), under which rules, what failed,
/// and the guest list as it would be saved.
/// </summary>
public sealed record BookingEvaluation(
    Space Space,
    Floor Floor,
    Building Building,
    BuildingClock LocalClock,
    ResolvedConstraints Rules,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    IReadOnlyList<BookingViolation> Violations,
    IReadOnlyList<BookingViolation> Warnings,
    IReadOnlyList<Invitee> Invitees,
    IReadOnlyDictionary<Guid, int>? BusyDates = null)
{
    public bool IsValid => Violations.Count == 0;
}
