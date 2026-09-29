namespace Dixels.SpaceManagement;

/// <summary>
/// Whether one person may hold two bookings at the same time in a building — say a desk
/// and a meeting room. A building setting (not a floor or space one): the two bookings can
/// be on different floors, so only the building can say which rule applies.
/// </summary>
public enum OwnOverlapPolicy
{
    /// <summary>Allowed, silently.</summary>
    Allow,

    /// <summary>Allowed, but the employee is told about the clash before booking.</summary>
    Warn,

    /// <summary>Refused: one booking at a time per person.</summary>
    Block,
}
