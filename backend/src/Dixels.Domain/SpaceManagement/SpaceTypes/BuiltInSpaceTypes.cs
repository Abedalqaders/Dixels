using System.Collections.Generic;

namespace Dixels.SpaceManagement;

/// <summary>A space type every installation starts with, named in each language it ships with.</summary>
public sealed record BuiltInSpaceType(string EnglishName, string ArabicName, IconKey IconKey);

/// <summary>
/// The 3 default space types from the original mock design. Seeded by
/// SpaceTypeDataSeedContributor, and looked up (by English name) by the hierarchy and demo
/// seeders — one definition, so their names can't drift apart.
/// </summary>
public static class BuiltInSpaceTypes
{
    public static readonly BuiltInSpaceType MeetingRoom = new("Meeting room", "غرفة اجتماعات", IconKey.MeetingRoom);
    public static readonly BuiltInSpaceType FocusPod = new("Focus pod", "كبسولة تركيز", IconKey.FocusPod);
    public static readonly BuiltInSpaceType Desk = new("Desk", "مكتب", IconKey.Desk);

    public static IReadOnlyList<BuiltInSpaceType> All { get; } = [MeetingRoom, FocusPod, Desk];
}
