using System;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace Dixels.Users;

/// <summary>
/// Typed access to the <see cref="DixelsUserConsts.BuildingIdPropertyName"/> extra property,
/// so callers never pass the property name string around themselves.
/// </summary>
public static class IdentityUserBuildingExtensions
{
    public static Guid? GetBuildingId(this IdentityUser user)
    {
        return user.GetProperty<Guid?>(DixelsUserConsts.BuildingIdPropertyName);
    }

    /// <summary>Null clears the assignment.</summary>
    public static void SetBuildingId(this IdentityUser user, Guid? buildingId)
    {
        user.SetProperty(DixelsUserConsts.BuildingIdPropertyName, buildingId);
    }
}
