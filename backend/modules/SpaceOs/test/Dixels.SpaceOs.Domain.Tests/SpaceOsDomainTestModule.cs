using Volo.Abp.Modularity;

namespace Dixels.SpaceOs;

[DependsOn(
    typeof(SpaceOsDomainModule),
    typeof(SpaceOsTestBaseModule)
)]
public class SpaceOsDomainTestModule : AbpModule
{

}
