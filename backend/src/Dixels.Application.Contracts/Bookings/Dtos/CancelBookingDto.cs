using System.ComponentModel.DataAnnotations;

namespace Dixels.Bookings;

public class CancelBookingDto
{
    /// <summary>Optional — why the booking is being cancelled, kept with the cancelled row.</summary>
    [StringLength(BookingConsts.MaxCancelReasonLength)]
    public string? Reason { get; set; }

    /// <summary>For a series booking: just this one, this and the following ones, or every upcoming one.</summary>
    public CancelScope Scope { get; set; } = CancelScope.This;
}
