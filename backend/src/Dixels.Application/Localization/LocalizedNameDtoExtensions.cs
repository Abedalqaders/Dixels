using System;
using System.Collections.Generic;
using System.Linq;

namespace Dixels.Localization;

/// <summary>Between an entity's name rows and the DTOs' "names" lists.</summary>
public static class LocalizedNameDtoExtensions
{
    /// <summary>Every name, one per language, in a stable order — what the edit form shows.</summary>
    public static List<LocalizedNameDto> ToNameDtos(this IEnumerable<NameTranslation> translations) =>
        translations
            .OrderBy(t => t.Language, StringComparer.Ordinal)
            .Select(t => new LocalizedNameDto { Language = t.Language, Name = t.Name })
            .ToList();

    /// <summary>The names as typed, for <see cref="LocalizedNameValidator"/>.</summary>
    public static IEnumerable<LocalizedName> ToNames(this IEnumerable<LocalizedNameDto> names) =>
        names.Select(n => new LocalizedName(n.Language, n.Name));
}
