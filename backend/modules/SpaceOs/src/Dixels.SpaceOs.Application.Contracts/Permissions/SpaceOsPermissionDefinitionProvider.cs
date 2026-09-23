using Dixels.SpaceOs.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dixels.SpaceOs.Permissions;

public class SpaceOsPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(SpaceOsPermissions.GroupName, L("Permission:SpaceOs"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SpaceOsResource>(name);
    }
}
