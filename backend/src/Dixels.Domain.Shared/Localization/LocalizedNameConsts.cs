namespace Dixels.Localization;

/// <summary>
/// Limits for a name stored once per language (see LocalizedName in Dixels.Domain) — shared
/// by the translation entities and the DTOs' validation attributes.
/// </summary>
public static class LocalizedNameConsts
{
    /// <summary>An ABP culture name: "en", "ar", "zh-Hans".</summary>
    public const int MaxLanguageLength = 10;

    /// <summary>The longest name in any one language — space types, buildings, floors, spaces.</summary>
    public const int MaxNameLength = 128;
}
