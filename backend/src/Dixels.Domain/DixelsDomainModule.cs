using System;
using Microsoft.Extensions.DependencyInjection;
using Dixels.Bookings;
using Dixels.MultiTenancy;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.Timing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.MailKit;
using Volo.Abp.Modularity;
using Volo.Abp.MultiLingualObjects;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace Dixels;

[DependsOn(
    typeof(DixelsDomainSharedModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpFeatureManagementDomainModule),
    typeof(AbpIdentityDomainModule),
    typeof(AbpOpenIddictDomainModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpSettingManagementDomainModule),
    typeof(AbpTenantManagementDomainModule),
    typeof(AbpEmailingModule),
    // Sends ABP's emails over SMTP with MailKit. Server and sender come from the
    // Abp.Mailing.* settings (env vars, see backend/.env.example); in dev that's smtp4dev.
    typeof(AbpMailKitModule),
    typeof(AbpMultiLingualObjectsModule)
)]
public class DixelsDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The app's languages: the web app's language menu, the "+ Add translation" list for
        // names (space types, buildings…) and the login pages all follow this list.
        // To add a language: one line here + Dixels.Domain.Shared/Localization/Dixels/<code>.json
        // with every key en.json has (a frontend test lists any that are missing).
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
            options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
        });

        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = MultiTenancyConsts.IsEnabled;
        });

        // "Bookings" section in appsettings (install-time settings, see BookingOptions).
        Configure<BookingOptions>(context.Services.GetConfiguration().GetSection("Bookings"));

        // BRS: every stored timestamp is UTC. With Kind = Utc, ABP's IClock returns UTC and
        // audit columns (CreationTime etc.) are written as UTC. Wall-clock values that must
        // NOT be shifted (a booking's building-local start/end in DTOs) opt out with
        // [DisableDateTimeNormalization].
        Configure<AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });
    }
}
