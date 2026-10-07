using System;
using Dixels.Localization;
using Volo.Abp;

namespace Dixels.SpaceManagement;

/// <summary>
/// A building's name in one language (see <see cref="NameTranslation"/>), and optionally its
/// street address in that language. Part of the <see cref="Building"/> aggregate: changed
/// only through it.
/// </summary>
public class BuildingTranslation : NameTranslation
{
    public Guid BuildingId { get; private set; }

    /// <summary>Where the building is, for guests who don't know it (invite emails, the .ics location). Null when not given.</summary>
    public string? Address { get; private set; }

    private BuildingTranslation()
    {
        // EF Core
    }

    internal BuildingTranslation(Guid buildingId, string language, string name)
        : base(language, name)
    {
        BuildingId = buildingId;
    }

    internal void SetAddress(string? address)
    {
        Address = string.IsNullOrWhiteSpace(address)
            ? null
            : Check.Length(address.Trim(), nameof(address), BuildingConsts.MaxAddressLength);
    }

    public override object[] GetKeys() => [BuildingId, Language];
}
