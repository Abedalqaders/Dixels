using System;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>
/// A space type's name in one language (see <see cref="NameTranslation"/>). Part of the
/// <see cref="SpaceType"/> aggregate: changed only through it.
/// </summary>
public class SpaceTypeTranslation : NameTranslation
{
    public Guid SpaceTypeId { get; private set; }

    /// <summary>
    /// A copy of the space type's own soft-delete flag. The unique index on (Language,
    /// NormalizedName) skips deleted rows, so a deleted type's names can be used again — the
    /// same as the old index on the type's Name did with its WHERE "IsDeleted" = false.
    /// (Deliberately not ISoftDelete: ABP would then hide these rows from the type they
    /// belong to.)
    /// </summary>
    public bool IsDeleted { get; private set; }

    private SpaceTypeTranslation()
    {
        // EF Core
    }

    internal SpaceTypeTranslation(Guid spaceTypeId, string language, string name)
        : base(language, name)
    {
        SpaceTypeId = spaceTypeId;
    }

    internal void MarkDeleted()
    {
        IsDeleted = true;
    }

    public override object[] GetKeys() => [SpaceTypeId, Language];
}
