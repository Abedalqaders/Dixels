using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dixels.Localization;

/// <summary>
/// Which plural form a count takes in a language — the CLDR rules the web app gets from
/// Intl.PluralRules, for the server's own counted texts ("Duration:Hours_one", "_few"…).
/// English has "one" and "other"; Arabic six forms; Russian 1 час, 2 часа, 5 часов.
///
/// Whole, non-negative counts only. A language missing from the table counts like English
/// ("one" for 1, else "other"), the rule most languages share. Tested against Intl's
/// answers in PluralRulesTests: add a language here and there together.
/// </summary>
public static class PluralRules
{
    private static readonly Dictionary<string, Func<long, string>> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = Arabic,
        ["fr"] = n => n is 0 or 1 ? "one" : Million(n) ? "many" : "other",
        ["pt"] = n => n is 0 or 1 ? "one" : Million(n) ? "many" : "other",
        ["es"] = n => n == 1 ? "one" : Million(n) ? "many" : "other",
        ["it"] = n => n == 1 ? "one" : Million(n) ? "many" : "other",
        ["fa"] = n => n is 0 or 1 ? "one" : "other",
        ["hi"] = n => n is 0 or 1 ? "one" : "other",
        ["ru"] = EastSlavic,
        ["uk"] = EastSlavic,
        ["pl"] = n => n == 1 ? "one" : TwoToFour(n) ? "few" : "many",
        ["cs"] = n => n == 1 ? "one" : n is >= 2 and <= 4 ? "few" : "other",
        ["ro"] = n => n == 1 ? "one" : n == 0 || n % 100 is >= 1 and <= 19 ? "few" : "other",
        ["he"] = n => n switch { 1 => "one", 2 => "two", _ => "other" },
        ["id"] = _ => "other",
        ["ms"] = _ => "other",
        ["vi"] = _ => "other",
        ["th"] = _ => "other",
        ["zh"] = _ => "other",
        ["ja"] = _ => "other",
        ["ko"] = _ => "other",
    };

    /// <summary>The form of <paramref name="count"/> in the current UI language: "one", "few", "other"…</summary>
    public static string FormOf(long count) => FormOf(CultureInfo.CurrentUICulture, count);

    public static string FormOf(CultureInfo culture, long count)
    {
        return Rules.TryGetValue(culture.TwoLetterISOLanguageName, out var rule)
            ? rule(count)
            : count == 1 ? "one" : "other";
    }

    private static string Arabic(long n) => (n, n % 100) switch
    {
        (0, _) => "zero",
        (1, _) => "one",
        (2, _) => "two",
        (_, >= 3 and <= 10) => "few",
        (_, >= 11 and <= 99) => "many",
        _ => "other",
    };

    /// <summary>Russian and Ukrainian: 1, 21, 101 "one"; 2–4, 22–24 "few"; the rest (5–20, 11–14…) "many".</summary>
    private static string EastSlavic(long n) => n % 10 == 1 && n % 100 != 11 ? "one" : TwoToFour(n) ? "few" : "many";

    /// <summary>Ends in 2–4, but not 12–14.</summary>
    private static bool TwoToFour(long n) => n % 10 is >= 2 and <= 4 && n % 100 is not (>= 12 and <= 14);

    /// <summary>A whole number of millions — French "1 000 000 de", Spanish "1 000 000 de".</summary>
    private static bool Million(long n) => n != 0 && n % 1_000_000 == 0;
}
