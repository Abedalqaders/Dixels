namespace Dixels.Bookings;

public class BookingViolationDto
{
    /// <summary>The error code, e.g. <c>Dixels:Bookings:TooLong</c> — stable for the UI to switch on.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>"Building", "Floor" or "Space" — which level set the rule; null when no level did.</summary>
    public string? Level { get; set; }

    /// <summary>Localized, ready to show: names the rule, quotes the limit, says what to do.</summary>
    public string Message { get; set; } = string.Empty;
}
