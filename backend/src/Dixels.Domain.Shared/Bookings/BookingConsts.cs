namespace Dixels.Bookings;

/// <summary>
/// Shared between the <c>Booking</c> entity's own validation (Domain) and its DTOs'
/// <c>[StringLength]</c> attributes (Application.Contracts) — one source of truth for both.
/// </summary>
public static class BookingConsts
{
    public const int MaxTitleLength = 128;
    public const int MaxIdempotencyKeyLength = 64;
    public const int MaxCancelReasonLength = 512;

    /// <summary>Used when the employee leaves the title blank — the title is optional.</summary>
    public const string DefaultTitle = "Booking";
}
