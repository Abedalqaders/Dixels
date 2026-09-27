using System.Linq;
using Dixels.SpaceManagement;

namespace Dixels.Bookings;

/// <summary>
/// The rules a booking was accepted under, as stored in
/// <see cref="Booking.ResolvedConstraintsJson"/>. Deliberately a flat record of plain
/// values rather than <see cref="ResolvedConstraints"/> itself: the stored JSON must stay
/// readable and stable even if the value objects behind it are later reshaped, and the
/// levels are written as names ("Floor"), not enum numbers that a reorder would silently
/// change.
/// </summary>
public sealed record BookingRuleSnapshot(
    string Timezone,
    int[] Days,
    string DaysSource,
    bool IsOpen24Hours,
    string Open,
    string Close,
    string HoursSource,
    int MaxDurationMinutes,
    string MaxDurationSource,
    int MaxHorizonDays,
    int MinLeadMinutes,
    int? MinAttendees,
    int? Capacity)
{
    public static BookingRuleSnapshot From(ResolvedConstraints rules)
    {
        var hours = rules.Hours.Value;

        return new BookingRuleSnapshot(
            rules.Timezone,
            rules.Days.Value.ToDayOfWeeks().Select(d => (int)d).ToArray(),
            rules.Days.Source.ToString(),
            hours.IsOpen24Hours,
            hours.Open.ToString("HH:mm"),
            hours.Close.ToString("HH:mm"),
            rules.Hours.Source.ToString(),
            rules.MaxDurationMinutes.Value,
            rules.MaxDurationMinutes.Source.ToString(),
            rules.MaxHorizonDays,
            rules.MinLeadMinutes,
            rules.MinAttendees,
            rules.Capacity);
    }
}
