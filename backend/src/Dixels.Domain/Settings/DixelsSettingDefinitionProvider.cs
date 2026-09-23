using Volo.Abp.Settings;

namespace Dixels.Settings;

public class DixelsSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(DixelsSettings.MySetting1));
    }
}
