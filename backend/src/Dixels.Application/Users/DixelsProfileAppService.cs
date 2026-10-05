using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using IdentityOptions = Microsoft.AspNetCore.Identity.IdentityOptions;

namespace Dixels.Users;

/// <summary>
/// ABP's own profile service (<c>/api/account/my-profile</c>, what My profile uses), with two
/// changes:
/// <list type="bullet">
/// <item>an update drops every extra property it's sent. ABP copies them onto the user, and none
/// is the user's to set — the building (<see cref="DixelsUserConsts.BuildingIdPropertyName"/>)
/// is an admin's call;</item>
/// <item>a wrong current password is refused with its own code
/// (<see cref="DixelsDomainErrorCodes.WrongCurrentPassword"/>) — ABP's has none — so the page can
/// say so under the right box.</item>
/// </list>
/// Username and email changes are turned off by setting (DixelsSettingDefinitionProvider).
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IProfileAppService), typeof(ProfileAppService), typeof(DixelsProfileAppService))]
public class DixelsProfileAppService : ProfileAppService
{
    public DixelsProfileAppService(IdentityUserManager userManager, IOptions<IdentityOptions> identityOptions)
        : base(userManager, identityOptions)
    {
    }

    public override Task<ProfileDto> UpdateAsync(UpdateProfileDto input)
    {
        input.ExtraProperties.Clear();
        return base.UpdateAsync(input);
    }

    public override async Task ChangePasswordAsync(ChangePasswordInput input)
    {
        // An account with no password yet sets its first one: there's nothing to check.
        var user = await UserManager.GetByIdAsync(CurrentUser.GetId());
        if (user.PasswordHash is not null && !await UserManager.CheckPasswordAsync(user, input.CurrentPassword ?? string.Empty))
        {
            throw new BusinessException(DixelsDomainErrorCodes.WrongCurrentPassword);
        }

        await base.ChangePasswordAsync(input);
    }
}
