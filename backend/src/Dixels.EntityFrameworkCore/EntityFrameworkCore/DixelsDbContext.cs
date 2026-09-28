using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace Dixels.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class DixelsDbContext :
    AbpDbContext<DixelsDbContext>,
    IIdentityDbContext,
    ITenantManagementDbContext
{
    public DbSet<SpaceType> SpaceTypes { get; set; }
    public DbSet<Building> Buildings { get; set; }
    public DbSet<Floor> Floors { get; set; }
    public DbSet<Space> Spaces { get; set; }
    public DbSet<AvailabilityOverride> AvailabilityOverrides { get; set; }
    public DbSet<Booking> Bookings { get; set; }

    #region Entities from the modules

    /* Notice: We only implemented IIdentityDbContext and ITenantManagementDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityDbContext and ITenantManagementDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    //Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }
    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

    public DixelsDbContext(DbContextOptions<DixelsDbContext> options)
        : base(options)
    {

    }

    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // SQLite (the unit-test database only) stores DateTimeOffset as text and can't compare
        // it in SQL, so "bookings/closures overlapping this window" queries fail to translate.
        // There, store it as UTC ticks — a plain integer that sorts and compares correctly.
        // Postgres keeps its native timestamptz. Every DateTimeOffset in this app is a UTC
        // instant, so dropping the offset loses nothing.
        if (Database.ProviderName == SqliteProviderName)
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
            configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<UtcTicksConverter>();
        }
    }

    private sealed class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
    {
        public UtcTicksConverter()
            : base(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero))
        {
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureFeatureManagement();
        builder.ConfigureTenantManagement();

        /* Configure your own tables/entities inside here */

        builder.ConfigureSpaceManagement();
        builder.ConfigureBookings();
    }
}
