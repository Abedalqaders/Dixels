using System.ComponentModel.DataAnnotations;

namespace Dixels.Bookings;

public class CreateBookingDto : BookingRequestDto
{
    /// <summary>
    /// Generated once by the client per booking attempt (e.g. when the form opens) and sent
    /// again on every retry of that attempt, so a retry can't create a second booking.
    /// </summary>
    [Required]
    [StringLength(BookingConsts.MaxIdempotencyKeyLength)]
    public string IdempotencyKey { get; set; } = string.Empty;
}
