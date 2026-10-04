using System.Collections.Generic;
using Dixels.Localization;

namespace Dixels;

/// <summary>Names as the create/update DTOs take them, for tests: <c>Names = En("Room A")</c>.</summary>
public static class TestNames
{
    /// <summary>Just an English name (the default language's, which is required).</summary>
    public static List<LocalizedNameDto> En(string name) => [new() { Language = "en", Name = name }];

    /// <summary>An English and an Arabic name.</summary>
    public static List<LocalizedNameDto> EnAr(string english, string arabic) =>
        [new() { Language = "en", Name = english }, new() { Language = "ar", Name = arabic }];
}
