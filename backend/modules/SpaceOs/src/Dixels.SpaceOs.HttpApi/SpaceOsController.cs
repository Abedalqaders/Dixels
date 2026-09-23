using Dixels.SpaceOs.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dixels.SpaceOs;

public abstract class SpaceOsController : AbpControllerBase
{
    protected SpaceOsController()
    {
        LocalizationResource = typeof(SpaceOsResource);
    }
}
