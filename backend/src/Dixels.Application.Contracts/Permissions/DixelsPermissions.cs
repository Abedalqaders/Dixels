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
}
