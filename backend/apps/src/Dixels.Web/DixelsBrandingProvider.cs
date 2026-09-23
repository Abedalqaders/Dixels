using Volo.Abp.Ui.Branding;
using Volo.Abp.DependencyInjection;
using Microsoft.Extensions.Localization;
using Dixels.Localization;

namespace Dixels.Web;

[Dependency(ReplaceServices = true)]
public class DixelsBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<DixelsResource> _localizer;

    public DixelsBrandingProvider(IStringLocalizer<DixelsResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
