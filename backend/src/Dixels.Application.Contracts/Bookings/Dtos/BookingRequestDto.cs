using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Timing;

namespace Dixels.Bookings;

/// <summary>
/// What an employee asks for. <see cref="LocalStart"/>/<see cref="LocalEnd"/> are wall-clock
/// times in the space's building timezone, sent without an offset
/// (<c>"2026-09-29T10:00:00"</c>) — the server resolves the building's zone and converts to
/// UTC, so the booker's own device timezone never matters. They opt out of ABP's
/// DateTime normalization, which would otherwise treat them as UTC instants.
/// </summary>
public class BookingRequestDto
{
    [Required]
    public Guid SpaceId { get; set; }

    [Required]
    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [Required]
    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }

    [Range(1, int.MaxValue)]
    public int Attendees { get; set; } = 1;

    [StringLength(BookingConsts.MaxTitleLength)]
    public string? Title { get; set; }

    /// <summary>Who else is invited. <see cref="Attendees"/> must be at least one more (the owner).</summary>
    [MaxLength(BookingConsts.MaxInvitees)]
    public List<InviteeDto> Invitees { get; set; } = new();
}
