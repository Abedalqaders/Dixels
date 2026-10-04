using System;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>
/// A floor's name in one language (see <see cref="NameTranslation"/>). Part of the
/// <see cref="Floor"/> aggregate: changed only through it.
/// </summary>
public class FloorTranslation : NameTranslation
{
    public Guid FloorId { get; private set; }

    private FloorTranslation()
    {
        // EF Core
    }

    internal FloorTranslation(Guid floorId, string language, string name)
        : base(language, name)
    {
        FloorId = floorId;
    }

    public override object[] GetKeys() => [FloorId, Language];
}
