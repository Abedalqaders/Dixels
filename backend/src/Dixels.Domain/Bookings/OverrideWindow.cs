using Dixels.SpaceManagement;

namespace Dixels.Bookings;

/// <summary>
/// The parts of an <see cref="AvailabilityOverride"/> the validator needs, decoupled from
/// the entity so the validator stays a plain function over plain data.
/// </summary>
public sealed record OverrideWindow(
    TimeRange Range,
    OverrideEffect Effect,
    OverrideScope Scope,
    ReasonCategory Reason,
    string? ReasonDetail)
{
    public static OverrideWindow From(AvailabilityOverride o) =>
        new(new TimeRange(o.StartsAt, o.EndsAt), o.Effect, o.Scope, o.ReasonCategory, o.ReasonDetail);
}
