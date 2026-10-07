using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Timing;

namespace Dixels.Bookings;

/// <summary>
/// What a calendar needs to draw one of my bookings — and nothing more. A month of these
/// is a few KB however many fields a booking grows; the rest (<see cref="BookingDto"/>)
/// loads when the booking is opened.
/// </summary>
public class BookingSummaryDto : EntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;

    /// <summary>On the building's wall clock, for display (not normalized to UTC).</summary>
    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }

    /// <summary>Where — the room's name, even if the room has since been deleted.</summary>
    public string SpaceName { get; set; } = string.Empty;

    /// <summary>Confirmed, or Cancelled (by an admin: still shown, struck through).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Set when this booking is one date of a recurring series.</summary>
    public Guid? SeriesId { get; set; }

    /// <summary>Someone else's booking I'm invited to (read-only), rather than my own.</summary>
    public bool IsInvited { get; set; }
}
