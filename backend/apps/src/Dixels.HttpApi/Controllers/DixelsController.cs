using Dixels.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dixels.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class DixelsController : AbpControllerBase
{
    protected DixelsController()
    {
        LocalizationResource = typeof(DixelsResource);
    }
}
