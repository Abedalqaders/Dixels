using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Dixels.Bookings;
using Dixels.Emails;
using Dixels.MultiTenancy;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
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
using Volo.Abp.VirtualFileSystem;

namespace Dixels;

[DependsOn(
    typeof(DixelsDomainSharedModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpBlobStoringFileSystemModule),
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

        // Files (profile pictures) are kept as files under BlobStoring:FileSystem:BasePath, e.g.
        // <BasePath>/host/profile-pictures/<user id>. In Docker that folder must be a volume, or
        // a rebuild loses them. Without the setting: App_Data/blobs next to where the app runs.
        var blobsPath = context.Services.GetConfiguration()["BlobStoring:FileSystem:BasePath"];
        if (string.IsNullOrWhiteSpace(blobsPath))
        {
            blobsPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "blobs");
        }

        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container =>
                container.UseFileSystem(fileSystem => fileSystem.BasePath = blobsPath));
        });

        // "Bookings" section in appsettings (install-time settings, see BookingOptions).
        Configure<BookingOptions>(context.Services.GetConfiguration().GetSection("Bookings"));

        // "Emails" section: where the links in emails point (see EmailOptions).
        Configure<EmailOptions>(context.Services.GetConfiguration().GetSection("Emails"));
        Configure<GuestLinkOptions>(context.Services.GetConfiguration().GetSection("GuestLinks"));

        // The email templates (Emails/Templates/*.tpl) are embedded in this assembly.
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DixelsDomainModule>();
        });

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
