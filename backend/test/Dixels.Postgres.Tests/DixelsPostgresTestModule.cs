using Volo.Abp.Data;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Dixels.EntityFrameworkCore;

namespace Dixels;

/// <summary>
/// Like DixelsEntityFrameworkCoreTestModule, but against the real Postgres from
/// <see cref="PostgresFixture"/> and with unit-of-work transactions left ON — the space
/// lock only means anything inside a transaction.
/// </summary>
[DependsOn(
    typeof(DixelsApplicationTestModule),
    typeof(DixelsEntityFrameworkCoreModule))]
public class DixelsPostgresTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });
        Configure<SettingManagementOptions>(options =>
        {
            options.SaveStaticSettingsToDatabase = false;
            options.IsDynamicSettingStoreEnabled = false;
        });

        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = PostgresFixture.ConnectionString;
        });
    }
}
