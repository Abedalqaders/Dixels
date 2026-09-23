using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dixels.SpaceOs.MongoDB;

[ConnectionStringName(SpaceOsDbProperties.ConnectionStringName)]
public interface ISpaceOsMongoDbContext : IAbpMongoDbContext
{
    /* Define mongo collections here. Example:
     * IMongoCollection<Question> Questions { get; }
     */
}
