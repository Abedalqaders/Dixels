using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.SpaceOs.EntityFrameworkCore;

[ConnectionStringName(SpaceOsDbProperties.ConnectionStringName)]
public interface ISpaceOsDbContext : IEfCoreDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * DbSet<Question> Questions { get; }
     */
}
