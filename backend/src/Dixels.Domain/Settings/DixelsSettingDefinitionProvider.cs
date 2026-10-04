using Volo.Abp.Emailing;
using Volo.Abp.Settings;

namespace Dixels.Settings;

public class DixelsSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // ABP stores the SMTP password encrypted, so it decrypts whatever value it reads.
        // Ours comes as plain text from an env var (SMTP_PASSWORD) and is never stored in
        // the database, so there's nothing to decrypt.
        context.GetOrNull(EmailSettingNames.Smtp.Password)!.IsEncrypted = false;
    }
}
