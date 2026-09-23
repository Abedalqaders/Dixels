using System;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace Dixels.SpaceOs.MongoDB;

[DependsOn(
    typeof(SpaceOsApplicationTestModule),
    typeof(SpaceOsMongoDbModule)
)]
public class SpaceOsMongoDbTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = MongoDbFixture.GetRandomConnectionString();
        });
    }
}
