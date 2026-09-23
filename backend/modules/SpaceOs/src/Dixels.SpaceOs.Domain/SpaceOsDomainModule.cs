using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dixels.SpaceOs;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(SpaceOsDomainSharedModule)
)]
public class SpaceOsDomainModule : AbpModule
{

}
