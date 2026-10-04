using Volo.Abp.Identity.Settings;
using Volo.Abp.Settings;

namespace Dixels.Settings;

public class DixelsSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // My profile edits a person's name and phone. Their username and email are what they
        // sign in with, so only an admin changes those: ABP's profile service then leaves them
        // as they are, whatever it's sent. (The admin's own user service doesn't read these.)
        context.GetOrNull(IdentitySettingNames.User.IsUserNameUpdateEnabled)!.DefaultValue = "false";
        context.GetOrNull(IdentitySettingNames.User.IsEmailUpdateEnabled)!.DefaultValue = "false";
    }
}
