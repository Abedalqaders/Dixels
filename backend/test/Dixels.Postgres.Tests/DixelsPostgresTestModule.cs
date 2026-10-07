using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Dixels.EntityFrameworkCore;

namespace Dixels;

/// <summary>
/// Like DixelsEntityFrameworkCoreTestModule, but against the real Postgres from
/// <see cref="PostgresFixture"/> and with unit-of-work transactions left ON — the space
/// lock only means anything inside a transaction. Distributed locks are Postgres advisory
/// locks, as in DixelsWebModule.
/// </summary>
[DependsOn(
    typeof(DixelsApplicationTestModule),
    typeof(DixelsEntityFrameworkCoreModule),
    typeof(AbpDistributedLockingModule))]
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

        // As DixelsEntityFrameworkCoreModule, plus SqlCapture for the query-plan tests.
        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context =>
            {
                context.UseNpgsql();
                context.DbContextOptions.AddInterceptors(SqlCapture.Instance);
            });
        });

        context.Services.AddSingleton<IDistributedLockProvider>(_ =>
            new PostgresDistributedSynchronizationProvider(PostgresFixture.ConnectionString));
    }
}
