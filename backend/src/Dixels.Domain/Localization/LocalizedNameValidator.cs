using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Localization;
using Volo.Abp.Settings;

namespace Dixels.Localization;

/// <summary>
/// The rules every "name in several languages" follows (space types now; buildings, floors
/// and spaces next), whatever entity it belongs to:
/// - each language is one of the app's (AbpLocalizationOptions.Languages) and appears once;
/// - the default language (ABP's Abp.Localization.DefaultLanguage setting) has a name — it's
///   what every language without its own name falls back to;
/// - names are trimmed, and an empty one just means "no name in that language".
/// Uniqueness is per entity type, so each manager checks it itself.
/// </summary>
public class LocalizedNameValidator : DomainService
{
    private readonly IOptions<AbpLocalizationOptions> _localizationOptions;
    private readonly ISettingProvider _settingProvider;

    public LocalizedNameValidator(IOptions<AbpLocalizationOptions> localizationOptions, ISettingProvider settingProvider)
    {
        _localizationOptions = localizationOptions;
        _settingProvider = settingProvider;
    }

    public async Task<string> GetDefaultLanguageAsync()
    {
        return await _settingProvider.GetOrNullAsync(LocalizationSettingNames.DefaultLanguage) ?? "en";
    }

    /// <summary>Whether <paramref name="language"/> is one of the app's languages.</summary>
    public bool IsAppLanguage(string language)
    {
        return _localizationOptions.Value.Languages.Any(l => l.CultureName == language);
    }

    /// <summary>The names, trimmed and without empty ones — or a BusinessException saying what's wrong.</summary>
    public async Task<IReadOnlyList<LocalizedName>> NormalizeAsync(IEnumerable<LocalizedName> names)
    {
        var languages = _localizationOptions.Value.Languages;
        var result = new List<LocalizedName>();
        var seen = new HashSet<string>();

        foreach (var entry in names)
        {
            var code = entry.Language?.Trim() ?? string.Empty;
            var language = languages.FirstOrDefault(l => l.CultureName == code)
                ?? throw new BusinessException(DixelsDomainErrorCodes.UnsupportedLanguage).WithData("language", code);

            if (!seen.Add(code))
            {
                throw new BusinessException(DixelsDomainErrorCodes.LanguageListedTwice).WithData("language", language.DisplayName);
            }

            var name = entry.Name?.Trim() ?? string.Empty;
            if (name.Length > 0)
            {
                result.Add(new LocalizedName(code, name));
            }
        }

        var defaultLanguage = await GetDefaultLanguageAsync();
        if (result.All(n => n.Language != defaultLanguage))
        {
            var displayName = languages.FirstOrDefault(l => l.CultureName == defaultLanguage)?.DisplayName ?? defaultLanguage;
            throw new BusinessException(DixelsDomainErrorCodes.DefaultLanguageNameRequired)
                .WithData("language", displayName);
        }

        return result;
    }
}
