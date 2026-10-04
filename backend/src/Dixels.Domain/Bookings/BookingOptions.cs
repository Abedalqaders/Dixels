namespace Dixels.Bookings;

/// <summary>
/// Company-wide booking settings, set once at install in appsettings (<c>"Bookings"</c>
/// section) rather than stored in the database — CONSTRAINTS.md: nobody edits these
/// through the UI.
/// </summary>
public class BookingOptions
{
    /// <summary>Start and end times must fall on this grid (local time), and it's also the shortest booking.</summary>
    public int SlotMinutes { get; set; } = 15;

    /// <summary>
    /// How long before a booking starts its owner is emailed a reminder; 0 sends none. A
    /// booking made closer to its start than this gets no reminder — its confirmation was
    /// just sent.
    /// </summary>
    public int ReminderLeadMinutes { get; set; } = 30;
}
