using System;
using Dixels.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiLingualObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// A space type's name in one language — every language is a row, English included (the
/// default language's row is required; see LocalizedNameValidator). Part of the
/// <see cref="SpaceType"/> aggregate: changed only through it.
/// </summary>
public class SpaceTypeTranslation : Entity, IObjectTranslation
{
    public Guid SpaceTypeId { get; private set; }

    /// <summary>ABP culture name, e.g. "en" or "ar". Settable only because ABP's interface demands it.</summary>
    public string Language { get; set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>
    /// Trimmed and upper-cased, so "Desk" and "desk " are the same name. The unique index is
    /// on (Language, NormalizedName).
    /// </summary>
    public string NormalizedName { get; private set; } = null!;

    /// <summary>
    /// A copy of the space type's own soft-delete flag. The unique index skips deleted rows,
    /// so a deleted type's names can be used again — the same as the old index on the type's
    /// Name did with its WHERE "IsDeleted" = false. (Deliberately not ISoftDelete: ABP would
    /// then hide these rows from the type they belong to.)
    /// </summary>
    public bool IsDeleted { get; private set; }

    private SpaceTypeTranslation()
    {
        // EF Core
    }

    internal SpaceTypeTranslation(Guid spaceTypeId, string language, string name)
    {
        SpaceTypeId = spaceTypeId;
        Language = Check.NotNullOrWhiteSpace(language, nameof(language), LocalizedNameConsts.MaxLanguageLength);
        SetName(name);
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name?.Trim(), nameof(name), SpaceTypeConsts.MaxNameLength);
        NormalizedName = Normalize(Name);
    }

    internal void MarkDeleted()
    {
        IsDeleted = true;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public override object[] GetKeys() => [SpaceTypeId, Language];
}
