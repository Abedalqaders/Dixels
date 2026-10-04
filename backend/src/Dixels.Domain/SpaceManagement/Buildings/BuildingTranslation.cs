using System;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>
/// A building's name in one language (see <see cref="NameTranslation"/>). Part of the
/// <see cref="Building"/> aggregate: changed only through it.
/// </summary>
public class BuildingTranslation : NameTranslation
{
    public Guid BuildingId { get; private set; }

    private BuildingTranslation()
    {
        // EF Core
    }

    internal BuildingTranslation(Guid buildingId, string language, string name)
        : base(language, name)
    {
        BuildingId = buildingId;
    }

    public override object[] GetKeys() => [BuildingId, Language];
}
