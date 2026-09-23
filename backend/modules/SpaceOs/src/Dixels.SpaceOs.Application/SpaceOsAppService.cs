using Dixels.SpaceOs.Localization;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceOs;

public abstract class SpaceOsAppService : ApplicationService
{
    protected SpaceOsAppService()
    {
        LocalizationResource = typeof(SpaceOsResource);
        ObjectMapperContext = typeof(SpaceOsApplicationModule);
    }
}
