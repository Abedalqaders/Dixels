using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dixels.SpaceOs;

[DependsOn(
    typeof(SpaceOsApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class SpaceOsHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(SpaceOsApplicationContractsModule).Assembly,
            SpaceOsRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SpaceOsHttpApiClientModule>();
        });

    }
}
