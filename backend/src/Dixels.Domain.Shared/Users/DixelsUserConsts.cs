namespace Dixels.Users;

/// <summary>
/// Names of the extra properties this app adds to ABP's <c>IdentityUser</c>. Registered in
/// <see cref="DixelsModuleExtensionConfigurator"/> and mapped to real columns in
/// <c>DixelsEfCoreEntityExtensionMappings</c> — one name shared by both.
/// </summary>
public static class DixelsUserConsts
{
    /// <summary>The one building a user can see and book in (null = not assigned yet).
    /// Stored as its own <c>AbpUsers.BuildingId</c> column, not inside the ExtraProperties
    /// JSON, so the Users list can filter by it in SQL.</summary>
    public const string BuildingIdPropertyName = "BuildingId";

    /// <summary>Not a stored property: the key the user list reads from its input's extra
    /// properties (<c>?ExtraProperties[Role]=employee</c>) to return only one role's members.
    /// ABP's own list has no role filter.</summary>
    public const string RoleFilterKey = "Role";
}
