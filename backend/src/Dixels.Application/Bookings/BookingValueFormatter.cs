using System;
using System.Globalization;
using System.Linq;
using Dixels.Localization;
using Dixels.SpaceManagement.ValueObjects;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;

namespace Dixels.Bookings;

/// <summary>
/// Words a value quoted inside a booking message in the reader's language — "Sunday" /
/// "الأحد", "Sun–Thu" / "الأحد إلى الخميس", "2h 30m" / "ساعتين و30 دقيقة", "24 hours" /
/// "24 ساعة". Kept in step with the frontend's <c>features/bookings/format.ts</c> and uses the
/// same translations, so a rule reads the same in the space list as in a rejection message.
/// </summary>
public class BookingValueFormatter : ITransientDependency
{
    private readonly IStringLocalizer<DixelsResource> _localizer;

    public BookingValueFormatter(IStringLocalizer<DixelsResource> localizer)
    {
        _localizer = localizer;
    }

    public string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        DayOfWeek day => BookingFormat.Weekday(day),
        OperatingDays days => Days(days),
        OperatingWindow hours => Hours(hours),
        TimeSpan length => Duration((int)length.TotalMinutes),
        DateOnly date => BookingFormat.Date(date),
        DateTime local => BookingFormat.DateTime(local),
        TimeOnly time => BookingFormat.Clock(time),
        Enum named => EnumName(named),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>"every day", a run like "Sun–Thu", or a list like "Sun, Tue, Thu".</summary>
    public string Days(OperatingDays days)
    {
        var list = days.ToDayOfWeeks().OrderBy(d => d).ToList();

        if (list.Count == 7)
        {
            return _localizer["Booking:EveryDay"];
        }

        if (list.Count == 0)
        {
            return _localizer["Booking:NoDays"];
        }

        if (list.Count >= 3 && (int)list[^1] - (int)list[0] == list.Count - 1)
        {
            return _localizer["Booking:DayRange"].Value
                .Replace("{from}", BookingFormat.ShortWeekday(list[0]))
                .Replace("{to}", BookingFormat.ShortWeekday(list[^1]));
        }

        return string.Join(_localizer["Booking:ListSeparator"].Value, list.Select(BookingFormat.ShortWeekday));
    }

    public string Hours(OperatingWindow window)
    {
        return window.IsOpen24Hours
            ? _localizer["Booking:Open24Hours"]
            : $"{BookingFormat.Clock(window.Open)}–{BookingFormat.Clock(window.Close)}";
    }

    /// <summary>"45 min", "2h", "2h 30m".</summary>
    public string Duration(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;

        return (hours, rest) switch
        {
            (0, _) => Plural("Duration:Minutes", rest),
            (_, 0) => Plural("Duration:Hours", hours),
            _ => _localizer["Duration:HoursMinutes"].Value
                .Replace("{hours}", Plural("Duration:Hours", hours))
                .Replace("{minutes}", Plural("Duration:MinutesShort", rest)),
        };
    }

    // "Enum:ReasonCategory.Holiday" — or the plain name if nothing translates it.
    private string EnumName(Enum value)
    {
        var text = _localizer[$"Enum:{value.GetType().Name}.{value}"];
        return text.ResourceNotFound ? value.ToString() : text.Value;
    }

    // The translations use i18next's plural suffixes (Duration:Hours_one, _two, _few…), which
    // the frontend picks with the language's CLDR rules — PluralRules is the same for the server.
    // A form a language file lacks falls back to _other.
    private string Plural(string key, int count)
    {
        var text = _localizer[$"{key}_{PluralRules.FormOf(count)}"];
        if (text.ResourceNotFound)
        {
            text = _localizer[$"{key}_other"];
        }

        return text.Value.Replace("{count}", count.ToString(CultureInfo.InvariantCulture));
    }
}
