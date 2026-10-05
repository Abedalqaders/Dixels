using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dixels.Localization;

/// <summary>
/// Which letters a name in a given language may use: an Arabic name Arabic letters, an
/// English name English (Latin) letters. Digits, spaces and punctuation are fine in any.
///
/// A name in a language not written in Latin letters may still hold short Latin codes —
/// "قسم IT", "غرفة B12": a run of capitals and digits, up to 6 long, standing on its own.
/// It needs at least one letter of its own alphabet, though, so "IT" alone isn't an Arabic
/// name. A name with no letters at all ("101") fits every language.
///
/// A language missing from <see cref="LanguageScripts"/> isn't checked. The web app runs the
/// same rule (nameLettersProblem in LocalizedNameField.tsx): change the two together.
/// </summary>
public static partial class NameAlphabet
{
    private const string Latin = "Latn";

    /// <summary>The Unicode ranges each alphabet's letters sit in (ISO 15924 script codes).</summary>
    private static readonly Dictionary<string, (int From, int To)[]> ScriptRanges = new()
    {
        [Latin] = [(0x0041, 0x005A), (0x0061, 0x007A), (0x00AA, 0x00AA), (0x00BA, 0x00BA), (0x00C0, 0x024F), (0x1E00, 0x1EFF), (0xA720, 0xA7FF)],
        ["Arab"] = [(0x0600, 0x06FF), (0x0750, 0x077F), (0x0870, 0x08FF), (0xFB50, 0xFDFF), (0xFE70, 0xFEFF)],
        ["Cyrl"] = [(0x0400, 0x052F), (0x1C80, 0x1C8F), (0x2DE0, 0x2DFF), (0xA640, 0xA69F)],
        ["Grek"] = [(0x0370, 0x03FF), (0x1F00, 0x1FFF)],
        ["Hebr"] = [(0x0590, 0x05FF), (0xFB1D, 0xFB4F)],
        ["Deva"] = [(0x0900, 0x097F), (0xA8E0, 0xA8FF)],
        ["Thai"] = [(0x0E00, 0x0E7F)],
    };

    /// <summary>The alphabet each language is written in, by its two-letter code — so "en-GB" is "en".</summary>
    private static readonly Dictionary<string, string> LanguageScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = Latin, ["fr"] = Latin, ["de"] = Latin, ["es"] = Latin, ["it"] = Latin, ["pt"] = Latin,
        ["nl"] = Latin, ["tr"] = Latin, ["id"] = Latin, ["ms"] = Latin, ["pl"] = Latin, ["sv"] = Latin,
        ["da"] = Latin, ["fi"] = Latin, ["nb"] = Latin, ["no"] = Latin, ["ro"] = Latin, ["cs"] = Latin,
        ["hu"] = Latin, ["vi"] = Latin, ["sw"] = Latin,
        ["ar"] = "Arab", ["fa"] = "Arab", ["ur"] = "Arab",
        ["ru"] = "Cyrl", ["uk"] = "Cyrl", ["bg"] = "Cyrl",
        ["el"] = "Grek",
        ["he"] = "Hebr",
        ["hi"] = "Deva", ["mr"] = "Deva", ["ne"] = "Deva",
        ["th"] = "Thai",
    };

    /// <summary>A short Latin code standing on its own: "IT", "B12", "3-01".</summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}])[A-Z0-9][A-Z0-9-]{0,5}(?![\p{L}\p{N}])")]
    private static partial Regex LatinCode();

    /// <summary>Whether <paramref name="name"/> uses only letters of <paramref name="language"/>'s alphabet (see the class).</summary>
    public static bool Fits(string language, string name)
    {
        if (ScriptOf(language) is not { } script)
        {
            return true;
        }

        var ranges = ScriptRanges[script];
        var withoutCodes = script == Latin ? name : LatinCode().Replace(name, " ");
        var letters = withoutCodes.Where(char.IsLetter).ToList();

        if (letters.Any(c => !ranges.Any(r => c >= r.From && c <= r.To)))
        {
            return false;
        }

        // Codes and nothing else ("IT" as the Arabic name) is a name in another language.
        return letters.Count > 0 || !name.Any(char.IsLetter);
    }

    /// <summary>Whether short Latin codes are let through: every language not written in Latin letters.</summary>
    public static bool AllowsLatinCodes(string language)
    {
        return ScriptOf(language) is { } script && script != Latin;
    }

    private static string? ScriptOf(string language)
    {
        return LanguageScripts.GetValueOrDefault(language.Split('-')[0]);
    }
}
