using System.Net;
using Localization.Resources.AbpUi;
using Dixels.Localization;
using Volo.Abp.Account;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.SettingManagement;

namespace Dixels;

[DependsOn(
    typeof(DixelsApplicationContractsModule),
    typeof(AbpAccountHttpApiModule),
    typeof(AbpIdentityHttpApiModule),
    typeof(AbpPermissionManagementHttpApiModule),
    typeof(AbpFeatureManagementHttpApiModule),
    typeof(AbpSettingManagementHttpApiModule)
    )]
public class DixelsHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ConfigureLocalization();
        ConfigureHttpStatusCodes();
    }

    // ABP answers every BusinessException with 403 unless told otherwise. A slot someone
    // else just took, or an idempotency key reused for a different request, is a conflict
    // with the current state of the server — 409, which the BRS asks for explicitly. A guest
    // asking for an organiser-only action on a booking they're invited to is a real
    // "you may not": 403.
    private void ConfigureHttpStatusCodes()
    {
        Configure<AbpExceptionHttpStatusCodeOptions>(options =>
        {
            options.Map(DixelsDomainErrorCodes.BookingOverlap, HttpStatusCode.Conflict);
            options.Map(DixelsDomainErrorCodes.BookingIdempotencyKeyReused, HttpStatusCode.Conflict);
            options.Map(DixelsDomainErrorCodes.BookingOrganiserOnly, HttpStatusCode.Forbidden);
            options.Map(DixelsDomainErrorCodes.BookingOnlyOrganiserCancels, HttpStatusCode.Forbidden);
            options.Map(DixelsDomainErrorCodes.BookingOrganiserCannotRespond, HttpStatusCode.Forbidden);
        });
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<DixelsResource>()
                .AddBaseTypes(
                    typeof(AbpUiResource)
                );
        });
    }
}
