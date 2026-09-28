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
