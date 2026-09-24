using Dixels.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dixels.Permissions;

public class DixelsPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var dixelsGroup = context.AddGroup(DixelsPermissions.GroupName, L("PermissionGroup:Dixels"));

        var buildings = dixelsGroup.AddPermission(DixelsPermissions.Buildings.Default, L("Permission:Buildings"));
        buildings.AddChild(DixelsPermissions.Buildings.Create, L("Permission:Create"));
        buildings.AddChild(DixelsPermissions.Buildings.Edit, L("Permission:Edit"));
        buildings.AddChild(DixelsPermissions.Buildings.Delete, L("Permission:Delete"));

        var floors = dixelsGroup.AddPermission(DixelsPermissions.Floors.Default, L("Permission:Floors"));
        floors.AddChild(DixelsPermissions.Floors.Create, L("Permission:Create"));
        floors.AddChild(DixelsPermissions.Floors.Edit, L("Permission:Edit"));
        floors.AddChild(DixelsPermissions.Floors.Delete, L("Permission:Delete"));

        var spaces = dixelsGroup.AddPermission(DixelsPermissions.Spaces.Default, L("Permission:Spaces"));
        spaces.AddChild(DixelsPermissions.Spaces.Create, L("Permission:Create"));
        spaces.AddChild(DixelsPermissions.Spaces.Edit, L("Permission:Edit"));
        spaces.AddChild(DixelsPermissions.Spaces.Delete, L("Permission:Delete"));

        var overrides = dixelsGroup.AddPermission(DixelsPermissions.Overrides.Default, L("Permission:Overrides"));
        overrides.AddChild(DixelsPermissions.Overrides.Create, L("Permission:Create"));
        overrides.AddChild(DixelsPermissions.Overrides.Delete, L("Permission:Delete"));

        var spaceTypes = dixelsGroup.AddPermission(DixelsPermissions.SpaceTypes.Default, L("Permission:SpaceTypes"));
        spaceTypes.AddChild(DixelsPermissions.SpaceTypes.Create, L("Permission:Create"));
        spaceTypes.AddChild(DixelsPermissions.SpaceTypes.Edit, L("Permission:Edit"));
        spaceTypes.AddChild(DixelsPermissions.SpaceTypes.Delete, L("Permission:Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DixelsResource>(name);
    }
}
