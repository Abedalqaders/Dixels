using Volo.Abp.Modularity;

namespace Dixels.SpaceOs;

[DependsOn(
    typeof(SpaceOsApplicationModule),
    typeof(SpaceOsDomainTestModule)
    )]
public class SpaceOsApplicationTestModule : AbpModule
{

}
