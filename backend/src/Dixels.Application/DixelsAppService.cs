using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;

namespace Dixels;

/* Inherit your application services from this class.
 */
public abstract class DixelsAppService : ApplicationService
{
    protected DixelsAppService()
    {
        LocalizationResource = typeof(DixelsResource);
    }

    /// <summary>
    /// Passes when the current user holds any one of <paramref name="permissionNames"/> — for
    /// reads several roles need (see <c>DixelsPermissions.Readers</c>), which a single
    /// <c>[Authorize(...)]</c> can't express. Refuses exactly as <c>[Authorize]</c> does
    /// (AbpAuthorizationException, 403 with a Volo.Authorization code), so clients treat both alike.
    /// </summary>
    protected async Task CheckAnyPermissionAsync(params string[] permissionNames)
    {
        var permissionChecker = LazyServiceProvider.LazyGetRequiredService<IPermissionChecker>();
        var result = await permissionChecker.IsGrantedAsync(permissionNames);
        if (!result.Result.Values.Any(grant => grant == PermissionGrantResult.Granted))
        {
            throw new AbpAuthorizationException(code: AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted);
        }
    }
}
