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

    /// <summary>
    /// The most dates one series may have. Keeps a preview (every date checked against
    /// every rule) quick, and a series of this size is already three months of workdays.
    /// </summary>
    public const int MaxSeriesOccurrences = 100;

    /// <summary>Shorter than a booking's key: each occurrence's key is the series key plus ":yyyyMMdd".</summary>
    public const int MaxSeriesIdempotencyKeyLength = 50;

    /// <summary>
    /// The most people one booking may invite. A series copies its list to every date, so the
    /// worst case is this times <see cref="MaxSeriesOccurrences"/> rows in one save.
    /// </summary>
    public const int MaxInvitees = 50;

    public const int MaxInviteeEmailLength = 256;
    public const int MaxInviteeNameLength = 128;

    /// <summary>A guest's calendar-event UID ("{43 random characters}@dixels").</summary>
    public const int MaxIcsUidLength = 64;
}
