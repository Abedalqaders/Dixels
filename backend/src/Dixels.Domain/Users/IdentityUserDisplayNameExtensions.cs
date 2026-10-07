using Volo.Abp.Identity;

namespace Dixels.Users;

public static class IdentityUserDisplayNameExtensions
{
    /// <summary>"Name Surname" as the person filled it in, or their user name when they gave neither.</summary>
    public static string DisplayName(this IdentityUser user)
    {
        var name = $"{user.Name} {user.Surname}".Trim();
        return name.Length > 0 ? name : user.UserName;
    }
}
