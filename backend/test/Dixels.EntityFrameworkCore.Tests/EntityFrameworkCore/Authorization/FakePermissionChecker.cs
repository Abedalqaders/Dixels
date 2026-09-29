using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;

namespace Dixels.EntityFrameworkCore.Authorization;

/// <summary>
/// A permission checker whose grants a test sets directly — the role under test, without
/// creating roles, users or grant rows. Anything not granted is refused, as for a real user
/// whose role lacks it. Registered as the one <see cref="IPermissionChecker"/> by
/// <see cref="DixelsAuthorizationTestModule"/>, so every <c>[Authorize(...)]</c> and every
/// <c>CheckAnyPermissionAsync</c> in the app services consults it.
/// </summary>
public class FakePermissionChecker : IPermissionChecker
{
    private readonly HashSet<string> _granted = new();

    /// <summary>Replaces the current grants with exactly these.</summary>
    public void Grant(params string[] names)
    {
        _granted.Clear();
        foreach (var name in names)
        {
            _granted.Add(name);
        }
    }

    public Task<bool> IsGrantedAsync(string name) => Task.FromResult(_granted.Contains(name));

    public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name) => IsGrantedAsync(name);

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
    {
        var result = new MultiplePermissionGrantResult();
        foreach (var name in names)
        {
            result.Result[name] = _granted.Contains(name) ? PermissionGrantResult.Granted : PermissionGrantResult.Prohibited;
        }

        return Task.FromResult(result);
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names) => IsGrantedAsync(names);
}
