namespace Dixels.SpaceManagement;

/// <summary>
/// A structured taxonomy for why an <see cref="AvailabilityOverride"/> exists, so the admin
/// UI can offer a dropdown instead of relying on free text alone.
/// </summary>
public enum ReasonCategory
{
    Maintenance,
    Holiday,
    Event,
    Other
}
