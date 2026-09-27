using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Timing;

namespace Dixels.Bookings;

/// <summary>
/// "What can I book for this window?" — the window is wall-clock time in the employee's
/// building (no offset, like <see cref="BookingRequestDto"/>) and must sit within one day.
/// </summary>
public class SearchAvailabilityInput
{
    [Required]
    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [Required]
    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }

    [Range(1, int.MaxValue)]
    public int Attendees { get; set; } = 1;

    public Guid? FloorId { get; set; }

    public Guid? SpaceTypeId { get; set; }
}
