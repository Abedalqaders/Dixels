using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.Bookings;

/// <summary>
/// Display formatting for the values quoted inside rejection messages ("at most 2h",
/// "open Sun–Thu", "07:00–20:00"). Invariant culture and a 24-hour clock, matching the rest
/// of the app.
/// </summary>
public static class BookingFormat
{
    private static readonly string[] ShortDayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    public static string Duration(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;

        return (hours, rest) switch
        {
            (0, _) => $"{rest} min",
            (_, 0) => $"{hours}h",
            _ => $"{hours}h {rest}m",
        };
    }

    public static string Hours(OperatingWindow window)
    {
        return window.IsOpen24Hours
            ? "24 hours"
            : $"{window.Open:HH\\:mm}–{window.Close:HH\\:mm}";
    }

    /// <summary>"every day", a run like "Sun–Thu", or a list like "Sun, Tue, Thu".</summary>
    public static string Days(OperatingDays days)
    {
        var list = days.ToDayOfWeeks().Select(d => (int)d).ToList();

        if (list.Count == 7)
        {
            return "every day";
        }

        if (list.Count == 0)
        {
            return "no days";
        }

        if (list.Count >= 3 && list[^1] - list[0] == list.Count - 1)
        {
            return $"{ShortDayNames[list[0]]}–{ShortDayNames[list[^1]]}";
        }

        return string.Join(", ", list.Select(d => ShortDayNames[d]));
    }

    public static string Day(DayOfWeek day) => CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(day);

    public static string Date(DateOnly date) => date.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime local) => local.ToString("ddd d MMM HH:mm", CultureInfo.InvariantCulture);

    internal static IReadOnlyDictionary<string, object> Data(params (string Key, object Value)[] pairs)
    {
        return pairs.ToDictionary(p => p.Key, p => p.Value);
    }
}
