using Volo.Abp.Emailing;
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

        // App-wide. Visible to clients, so the booking form can hide the "External guest" tab.
        context.Add(new SettingDefinition(DixelsSettings.ExternalGuestsEnabled, defaultValue: "true", isVisibleToClients: true));

        // App-wide, server-only.
        context.Add(new SettingDefinition(DixelsSettings.RsvpMailboxAddress, defaultValue: "rsvp@dixels.local"));
    }
}
