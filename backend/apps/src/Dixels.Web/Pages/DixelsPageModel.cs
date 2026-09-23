using Dixels.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Dixels.Web.Pages;

public abstract class DixelsPageModel : AbpPageModel
{
    protected DixelsPageModel()
    {
        LocalizationResourceType = typeof(DixelsResource);
    }
}
