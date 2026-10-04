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

        // Only ever set per user: there's no app-wide value, ABP's DefaultLanguage is that.
        // A separate setting rather than a per-user Abp.Localization.DefaultLanguage, because
        // ABP reads that one once for the whole app's request culture and would pick up
        // whichever user happened to make the first request.
        context.Add(new SettingDefinition(DixelsSettings.Language)
            .WithProviders(UserSettingValueProvider.ProviderName));
    }
}
