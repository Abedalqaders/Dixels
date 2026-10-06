using System.Collections.Generic;

namespace Dixels.SpaceManagement;

/// <summary>
/// A space type every installation starts with, named in each language it ships with. A
/// new app language gets its names by adding them here; a language the app doesn't offer
/// is skipped when seeding.
/// </summary>
public sealed record BuiltInSpaceType(IReadOnlyDictionary<string, string> Names, IconKey IconKey)
{
    /// <summary>The English name — how the seeders find the type again.</summary>
    public string EnglishName => Names["en"];
}

/// <summary>
/// The 3 default space types from the original mock design. Seeded by
/// SpaceTypeDataSeedContributor, and looked up (by English name) by the hierarchy and demo
/// seeders — one definition, so their names can't drift apart.
/// </summary>
public static class BuiltInSpaceTypes
{
    public static readonly BuiltInSpaceType MeetingRoom = new(
        new Dictionary<string, string> { ["en"] = "Meeting room", ["ar"] = "غرفة اجتماعات" },
        IconKey.MeetingRoom);

    public static readonly BuiltInSpaceType FocusPod = new(
        new Dictionary<string, string> { ["en"] = "Focus pod", ["ar"] = "كبسولة تركيز" },
        IconKey.FocusPod);

    public static readonly BuiltInSpaceType Desk = new(
        new Dictionary<string, string> { ["en"] = "Desk", ["ar"] = "مكتب" },
        IconKey.Desk);

    public static IReadOnlyList<BuiltInSpaceType> All { get; } = [MeetingRoom, FocusPod, Desk];
}
