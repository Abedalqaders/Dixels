using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.SpaceOs.EntityFrameworkCore;

[ConnectionStringName(SpaceOsDbProperties.ConnectionStringName)]
public class SpaceOsDbContext : AbpDbContext<SpaceOsDbContext>, ISpaceOsDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * public DbSet<Question> Questions { get; set; }
     */

    public SpaceOsDbContext(DbContextOptions<SpaceOsDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureSpaceOs();
    }
}
