using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// The full set of effective constraints for a Floor or Space, each field resolved
/// independently (columns never travel in groups — a space can narrow hours alone and keep
/// the building's days).
///
/// Extension point for later: a future <c>bookings</c> table would persist this resolved
/// snapshot on the booking row at creation time (grandfathering — tightening a constraint
/// never retroactively invalidates an existing booking). No such table exists yet, so there
/// is no enforcement to build here now.
/// </summary>
public sealed record ResolvedConstraints(
    string Timezone,
    FieldValue<OperatingDays> Days,
    FieldValue<OperatingWindow> Hours,
    FieldValue<int> MaxDurationMinutes,
    int MaxHorizonDays,
    int MinLeadMinutes,
    int? MinAttendees,
    int? Capacity);
