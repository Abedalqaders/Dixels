namespace Dixels.SpaceManagement;

/// <summary>
/// A small, fixed icon set a <see cref="SpaceType"/> picks from. Keeping this a closed
/// set (rather than an arbitrary icon name/URL) means a custom, admin-added space type
/// still renders an intentional icon instead of always falling back to a generic box.
/// </summary>
public enum IconKey
{
    MeetingRoom,
    FocusPod,
    Desk,
    Generic
}
