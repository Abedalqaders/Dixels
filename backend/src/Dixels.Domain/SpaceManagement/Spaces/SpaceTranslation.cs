using System;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>
/// A space's name in one language (see <see cref="NameTranslation"/>). Part of the
/// <see cref="Space"/> aggregate: changed only through it.
/// </summary>
public class SpaceTranslation : NameTranslation
{
    public Guid SpaceId { get; private set; }

    private SpaceTranslation()
    {
        // EF Core
    }

    internal SpaceTranslation(Guid spaceId, string language, string name)
        : base(language, name)
    {
        SpaceId = spaceId;
    }

    public override object[] GetKeys() => [SpaceId, Language];
}
