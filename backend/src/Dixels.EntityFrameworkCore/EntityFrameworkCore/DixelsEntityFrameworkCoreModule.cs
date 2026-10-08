using Dixels.Bookings;
using Dixels.SpaceManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Uow;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DependencyInjection;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace Dixels.EntityFrameworkCore;

[DependsOn(
    typeof(DixelsDomainModule),
    typeof(AbpIdentityEntityFrameworkCoreModule),
    typeof(AbpOpenIddictEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    typeof(AbpSettingManagementEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(AbpBackgroundJobsEntityFrameworkCoreModule),
    typeof(AbpAuditLoggingEntityFrameworkCoreModule),
    typeof(AbpTenantManagementEntityFrameworkCoreModule),
    typeof(AbpFeatureManagementEntityFrameworkCoreModule)
    )]
public class DixelsEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        DixelsEfCoreEntityExtensionMappings.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<DixelsDbContext>(options =>
        {
                /* Remove "includeAllEntities: true" to create
                 * default repositories only for aggregate roots */
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<Booking, EfCoreBookingRepository>();
            options.AddRepository<SpaceType, EfCoreSpaceTypeRepository>();
        });

        // A named entity is never shown without its names: GetAsync/FindAsync (and
        // GetListAsync(includeDetails: true)) load them.
        Configure<AbpEntityOptions>(options =>
        {
            options.Entity<SpaceType>(o => o.DefaultWithDetailsFunc = q => q.Include(t => t.Translations));
            options.Entity<Building>(o => o.DefaultWithDetailsFunc = q => q.Include(b => b.Translations));
            options.Entity<Floor>(o => o.DefaultWithDetailsFunc = q => q.Include(f => f.Translations));
            options.Entity<Space>(o => o.DefaultWithDetailsFunc = q => q.Include(s => s.Translations));

            // Likewise a booking (or series) and its guests. Lists (calendar, overlap checks)
            // don't ask for details, so they don't pay for the join.
            options.Entity<Booking>(o => o.DefaultWithDetailsFunc = q => q.Include(b => b.Invitees));
            options.Entity<BookingSeries>(o => o.DefaultWithDetailsFunc = q => q.Include(s => s.Invitees));
        });

        Configure<AbpDbContextOptions>(options =>
        {
                /* The main point to change your DBMS.
                 * See also DixelsMigrationsDbContextFactory for EF Core tooling. */
            options.UseNpgsql();
        });

    }
}
