namespace Dixels.Bookings;

/// <summary>
/// A stretch of the searched day on the building's wall clock, as minutes from local
/// midnight (0–1440) — ready to draw on a day bar without any timezone math on the client.
/// </summary>
public class DayRangeDto
{
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }

    /// <summary>For busy ranges: the booking is the current user's own. Other people's bookings are anonymous.</summary>
    public bool IsMine { get; set; }
}
