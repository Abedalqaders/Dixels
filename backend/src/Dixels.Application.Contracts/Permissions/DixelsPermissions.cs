namespace Dixels.Permissions;

public static class DixelsPermissions
{
    public const string GroupName = "Dixels";

    public static class Buildings
    {
        public const string Default = GroupName + ".Buildings";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Floors
    {
        public const string Default = GroupName + ".Floors";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Spaces
    {
        public const string Default = GroupName + ".Spaces";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    // No Edit: an AvailabilityOverride is managed as delete+recreate, never edited in place.
    public static class Overrides
    {
        public const string Default = GroupName + ".Overrides";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class SpaceTypes
    {
        public const string Default = GroupName + ".SpaceTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    // No Users permissions here: listing users and assigning their building go through ABP's
    // own user service, so ABP's AbpIdentity.Users(.Update) permissions cover them.

    /// <summary>
    /// Who may <em>read</em> a level, beyond its own Default: "see the path, act only where
    /// granted". The hierarchy is a tree, so someone given floors (or spaces) must be able to
    /// list the buildings (and floors) above to reach them — read-only; creating, editing and
    /// deleting still need that level's own permission. The frontend mirrors these lists.
    /// </summary>
    public static class Readers
    {
        /// <summary>Listing/getting buildings: their own viewers, anyone below them in the tree,
        /// and user admins (the Users page's building picker).</summary>
        public static readonly string[] Buildings =
        {
            DixelsPermissions.Buildings.Default,
            DixelsPermissions.Floors.Default,
            DixelsPermissions.Spaces.Default,
            "AbpIdentity.Users",
        };

        /// <summary>Listing/getting floors: their own viewers and space viewers below them.</summary>
        public static readonly string[] Floors =
        {
            DixelsPermissions.Floors.Default,
            DixelsPermissions.Spaces.Default,
        };

        /// <summary>Listing space types: their own viewers, and space viewers — every space has one,
        /// and adding or filtering spaces picks from the list.</summary>
        public static readonly string[] SpaceTypes =
        {
            DixelsPermissions.SpaceTypes.Default,
            DixelsPermissions.Spaces.Default,
        };
    }

    // Employees get Default + Create + Cancel through the "employee" role (RoleDataSeedContributor).
    // ManageAll is for administrators acting on other people's bookings (force cancel) —
    // defined now so that feature is a grant, not a new permission, when it lands.
    public static class Bookings
    {
        public const string Default = GroupName + ".Bookings";
        public const string Create = Default + ".Create";
        public const string Cancel = Default + ".Cancel";
        public const string ManageAll = Default + ".ManageAll";
    }
}
