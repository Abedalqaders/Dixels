using Volo.Abp.Modularity;

namespace Dixels;

[DependsOn(
    typeof(DixelsApplicationModule),
    typeof(DixelsDomainTestModule)
)]
public class DixelsApplicationTestModule : AbpModule
{

}
