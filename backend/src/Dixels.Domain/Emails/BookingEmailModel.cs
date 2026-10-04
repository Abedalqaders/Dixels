namespace Dixels.Emails;

/// <summary>
/// What a booking email shows, already worded in the recipient's language. Templates read it
/// as <c>model.space_name</c> etc. (Scriban's snake_case names for these properties).
/// </summary>
public class BookingEmailModel
{
    public string RecipientName { get; set; } = string.Empty;
    public string SpaceName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>"Fri 2 Oct 2026", or for a series "Fri 2 Oct 2026 – Fri 30 Oct 2026". Building-local.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>"10:00–11:00", building-local.</summary>
    public string Time { get; set; } = string.Empty;

    public int Attendees { get; set; }
    public string? Title { get; set; }

    /// <summary>How many bookings the email is about: 1, or a series' dates.</summary>
    public int Count { get; set; }

    /// <summary>A cancellation: why, if the employee said.</summary>
    public string? Reason { get; set; }

    /// <summary>Where "Open Dixels" goes.</summary>
    public string AppUrl { get; set; } = string.Empty;
}
