using Dixels.Localization;
using Volo.Abp.Application.Services;

namespace Dixels;

/* Inherit your application services from this class.
 */
public abstract class DixelsAppService : ApplicationService
{
    protected DixelsAppService()
    {
        LocalizationResource = typeof(DixelsResource);
    }
}
