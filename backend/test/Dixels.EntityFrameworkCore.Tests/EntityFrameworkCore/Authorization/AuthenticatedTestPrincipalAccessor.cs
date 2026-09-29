using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Security.Claims;

namespace Dixels.EntityFrameworkCore.Authorization;

/// <summary>
/// The signed-in user for the authorization tests. The test base's own fake principal has no
/// authentication type, which the real authorization service reads as "anonymous" — and a
/// bare <c>[Authorize]</c> would then refuse everything before any permission is looked at.
/// Same user as the base fake otherwise, so nothing else in the tests notices.
/// </summary>
public class AuthenticatedTestPrincipalAccessor : ThreadCurrentPrincipalAccessor
{
    protected override ClaimsPrincipal GetClaimsPrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim>
            {
                new Claim(AbpClaimTypes.UserId, "2e701e62-0953-4dd3-910b-dc6cc93ccb0d"),
                new Claim(AbpClaimTypes.UserName, "admin"),
                new Claim(AbpClaimTypes.Email, "admin@abp.io"),
            },
            authenticationType: "Test"));
    }
}
