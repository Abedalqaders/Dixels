using Localization.Resources.AbpUi;
using Dixels.SpaceOs.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dixels.SpaceOs;

[DependsOn(
    typeof(SpaceOsApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class SpaceOsHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(SpaceOsHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<SpaceOsResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
