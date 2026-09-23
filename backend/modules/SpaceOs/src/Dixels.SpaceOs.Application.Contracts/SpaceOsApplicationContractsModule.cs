using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace Dixels.SpaceOs;

[DependsOn(
    typeof(SpaceOsDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class SpaceOsApplicationContractsModule : AbpModule
{

}
