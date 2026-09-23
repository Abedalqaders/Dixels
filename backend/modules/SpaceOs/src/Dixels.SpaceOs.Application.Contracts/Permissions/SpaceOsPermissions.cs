using Volo.Abp.Reflection;

namespace Dixels.SpaceOs.Permissions;

public class SpaceOsPermissions
{
    public const string GroupName = "SpaceOs";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(SpaceOsPermissions));
    }
}
