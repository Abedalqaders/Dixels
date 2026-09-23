using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dixels.SpaceOs.MongoDB;

[ConnectionStringName(SpaceOsDbProperties.ConnectionStringName)]
public class SpaceOsMongoDbContext : AbpMongoDbContext, ISpaceOsMongoDbContext
{
    /* Add mongo collections here. Example:
     * public IMongoCollection<Question> Questions => Collection<Question>();
     */

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureSpaceOs();
    }
}
