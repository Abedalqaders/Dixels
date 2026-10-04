using System;
using System.Collections.Generic;
using System.Linq;

namespace Dixels.Localization;

/// <summary>
/// The name operations every multi-lingual entity offers (SetName, RemoveName, FindName,
/// SetNames), written once over its Translations collection.
/// </summary>
public static class NameTranslationCollectionExtensions
{
    /// <summary>The name in exactly this language, if there is one (no fallback).</summary>
    public static string? FindName<T>(this IEnumerable<T> translations, string language)
        where T : NameTranslation =>
        translations.FirstOrDefault(t => t.Language == language)?.Name;

    /// <summary>Adds or changes the name in one language; <paramref name="create"/> makes a new row.</summary>
    public static void SetName<T>(this ICollection<T> translations, string language, string name, Func<string, string, T> create)
        where T : NameTranslation
    {
        var existing = translations.FirstOrDefault(t => t.Language == language);
        if (existing is null)
        {
            translations.Add(create(language, name));
        }
        else
        {
            existing.SetName(name);
        }
    }

    public static void RemoveName<T>(this ICollection<T> translations, string language)
        where T : NameTranslation
    {
        var existing = translations.FirstOrDefault(t => t.Language == language);
        if (existing is not null)
        {
            translations.Remove(existing);
        }
    }

    /// <summary>
    /// Makes the names exactly <paramref name="names"/> (already validated): languages left
    /// out lose their name, the rest are added or changed in place.
    /// </summary>
    public static void SetNames<T>(this ICollection<T> translations, IReadOnlyCollection<LocalizedName> names, Func<string, string, T> create)
        where T : NameTranslation
    {
        foreach (var dropped in translations.Where(t => names.All(n => n.Language != t.Language)).ToList())
        {
            translations.Remove(dropped);
        }

        foreach (var name in names)
        {
            translations.SetName(name.Language, name.Name, create);
        }
    }
}
