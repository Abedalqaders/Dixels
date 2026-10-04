using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dixels.Bookings;

/// <summary>
/// Dates and weekday names in the reader's language (the request's UI culture), worded the
/// way the frontend's <c>lib/time/format.ts</c> words them: "Fri 2 Oct 2026" in English,
/// "الجمعة 2 أكتوبر 2026" in Arabic — always the Gregorian calendar, Western digits and a
/// 24-hour clock. English uses the invariant names, as the frontend's fixed table does (ICU's
/// English short month for September is "Sep" or "Sept" depending on the version).
///
/// Rejection rules don't word their values here: a <see cref="BookingViolation"/> carries the
/// raw values and the application layer words them when it builds the message. This is for
/// the few messages the domain throws as text itself.
/// </summary>
public static class BookingFormat
{
    private static readonly ConcurrentDictionary<string, DateTimeFormatInfo> NamesByCulture = new();

    public static string Weekday(DayOfWeek day) => Names().GetDayName(day);

    /// <summary>"Sun"; in Arabic the full name — it has no three-letter abbreviations.</summary>
    public static string ShortWeekday(DayOfWeek day) => Names().GetAbbreviatedDayName(day);

    public static string Date(DateOnly date) => date.ToString("ddd d MMM yyyy", Names());

    public static string DateTime(DateTime local) => local.ToString("ddd d MMM HH:mm", Names());

    public static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    internal static IReadOnlyDictionary<string, object> Data(params (string Key, object Value)[] pairs)
    {
        return pairs.ToDictionary(p => p.Key, p => p.Value);
    }

    private static DateTimeFormatInfo Names()
    {
        var culture = CultureInfo.CurrentUICulture;
        if (culture.TwoLetterISOLanguageName is "en" or "iv")
        {
            return CultureInfo.InvariantCulture.DateTimeFormat;
        }

        return NamesByCulture.GetOrAdd(culture.Name, _ => Gregorian(culture));
    }

    // Some cultures default to another calendar (ar-SA to Umm al-Qura); dates in this app are
    // Gregorian everywhere, so switch to the culture's own Gregorian names.
    private static DateTimeFormatInfo Gregorian(CultureInfo culture)
    {
        var format = (DateTimeFormatInfo)culture.DateTimeFormat.Clone();
        if (format.Calendar is not GregorianCalendar)
        {
            var gregorian = culture.OptionalCalendars.OfType<GregorianCalendar>()
                .OrderBy(c => c.CalendarType == GregorianCalendarTypes.Localized ? 0 : 1)
                .FirstOrDefault();
            if (gregorian is not null)
            {
                format.Calendar = gregorian;
            }
        }

        return format;
    }
}
