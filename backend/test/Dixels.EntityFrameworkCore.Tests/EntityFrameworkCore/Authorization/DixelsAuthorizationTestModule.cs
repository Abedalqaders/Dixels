using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;

namespace Dixels.EntityFrameworkCore.Authorization;

/// <summary>
/// The EF Core test application with authorization switched back on. The test base replaces
/// ABP's authorization services with always-allow stand-ins (so ordinary tests never fight
/// permissions); this module puts the real ones back and supplies a
/// <see cref="FakePermissionChecker"/> the test fills in — the only way to prove that a role
/// is <em>refused</em> what it wasn't granted.
/// </summary>
[DependsOn(typeof(DixelsEntityFrameworkCoreTestModule))]
public class DixelsAuthorizationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // Undo AddAlwaysAllowAuthorization: the three services it stubs, back to ABP's own.
        services.Replace(ServiceDescriptor.Transient<IAuthorizationService, AbpAuthorizationService>());
        services.Replace(ServiceDescriptor.Transient<IAbpAuthorizationService, AbpAuthorizationService>());
        services.Replace(ServiceDescriptor.Transient<IMethodInvocationAuthorizationService, MethodInvocationAuthorizationService>());

        // ...and the permission checker they consult, as one instance the test can set grants on.
        services.AddSingleton<FakePermissionChecker>();
        services.Replace(ServiceDescriptor.Singleton<IPermissionChecker>(sp => sp.GetRequiredService<FakePermissionChecker>()));

        services.Replace(ServiceDescriptor.Singleton<ICurrentPrincipalAccessor, AuthenticatedTestPrincipalAccessor>());
    }
}
