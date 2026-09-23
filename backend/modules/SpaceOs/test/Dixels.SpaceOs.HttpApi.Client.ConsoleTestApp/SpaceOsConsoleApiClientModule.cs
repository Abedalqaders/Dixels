using Volo.Abp.Autofac;
using Volo.Abp.Http.Client.IdentityModel;
using Volo.Abp.Modularity;

namespace Dixels.SpaceOs;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(SpaceOsHttpApiClientModule),
    typeof(AbpHttpClientIdentityModelModule)
    )]
public class SpaceOsConsoleApiClientModule : AbpModule
{

}
