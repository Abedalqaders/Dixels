using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.SpaceOs.EntityFrameworkCore;

public class SpaceOsHttpApiHostMigrationsDbContext : AbpDbContext<SpaceOsHttpApiHostMigrationsDbContext>
{
    public SpaceOsHttpApiHostMigrationsDbContext(DbContextOptions<SpaceOsHttpApiHostMigrationsDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigureSpaceOs();
    }
}
