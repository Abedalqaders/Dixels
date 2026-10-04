using System;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.SettingManagement;

namespace Dixels.Users;

/// <summary>
/// The language each user uses the app in, so what the app sends them on its own — booking
/// emails — reaches them in it. The web app saves it whenever it's signed in, so it's the
/// language they last used, on whichever device.
/// </summary>
public class UserLanguageManager : DomainService
{
    private readonly ISettingManager _settingManager;
    private readonly LocalizedNameValidator _languages;

    public UserLanguageManager(ISettingManager settingManager, LocalizedNameValidator languages)
    {
        _settingManager = settingManager;
        _languages = languages;
    }

    public async Task SetAsync(Guid userId, string language)
    {
        if (!_languages.IsAppLanguage(language))
        {
            throw new BusinessException(DixelsDomainErrorCodes.UnsupportedLanguage).WithData("language", language);
        }

        // The web app sends it on every visit; only a change is worth a write.
        if (await _settingManager.GetOrNullForUserAsync(DixelsSettings.Language, userId, fallback: false) != language)
        {
            await _settingManager.SetForUserAsync(userId, DixelsSettings.Language, language);
        }
    }

    /// <summary>
    /// Their language — or the app's default language if they never signed in to the web app,
    /// or theirs has since been removed from the app.
    /// </summary>
    public async Task<string> GetAsync(Guid userId)
    {
        var saved = await _settingManager.GetOrNullForUserAsync(DixelsSettings.Language, userId, fallback: false);
        return saved is not null && _languages.IsAppLanguage(saved)
            ? saved
            : await _languages.GetDefaultLanguageAsync();
    }
}
