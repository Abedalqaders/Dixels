using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiLingualObjects;

namespace Dixels.Localization;

/// <summary>
/// An entity's name in one language — every language is a row, the default language's
/// included (it's required; see <see cref="LocalizedNameValidator"/>). The base of each
/// "XxxTranslation" table (space types, buildings, floors, spaces); each adds the key of the
/// entity it names. Changed only through that entity.
/// </summary>
public abstract class NameTranslation : Entity, IObjectTranslation
{
    /// <summary>ABP culture name, e.g. "en" or "ar". Settable only because ABP's interface demands it.</summary>
    public string Language { get; set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>Trimmed and upper-cased, so "Desk" and "desk " compare equal — for uniqueness and search.</summary>
    public string NormalizedName { get; private set; } = null!;

    protected NameTranslation()
    {
        // EF Core
    }

    protected NameTranslation(string language, string name)
    {
        Language = Check.NotNullOrWhiteSpace(language, nameof(language), LocalizedNameConsts.MaxLanguageLength);
        SetName(name);
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name?.Trim(), nameof(name), LocalizedNameConsts.MaxNameLength);
        NormalizedName = Normalize(Name);
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
