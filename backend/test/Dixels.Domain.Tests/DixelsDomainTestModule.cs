using Volo.Abp.Modularity;

namespace Dixels;

[DependsOn(
    typeof(DixelsDomainModule),
    typeof(DixelsTestBaseModule)
)]
public class DixelsDomainTestModule : AbpModule
{

}
