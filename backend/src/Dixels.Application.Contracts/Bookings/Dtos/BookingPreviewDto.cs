using System;
using System.Collections.Generic;

namespace Dixels.Bookings;

/// <summary>
/// The dry-run verdict. Produced by the exact same code path as a real create, so a
/// preview that says <see cref="IsValid"/> means the create would accept it — unless someone
/// else books the slot first (a preview is not a reservation).
/// </summary>
public class BookingPreviewDto
{
    public bool IsValid { get; set; }

    /// <summary>Every broken rule, most fundamental first.</summary>
    public List<BookingViolationDto> Violations { get; set; } = new();

    /// <summary>Things worth knowing that don't stop the booking — e.g. you already have another room then.</summary>
    public List<BookingViolationDto> Warnings { get; set; } = new();

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Timezone { get; set; } = string.Empty;

    /// <summary>The guest list as it would be saved: a typed colleague's email already shown as that colleague.</summary>
    public List<BookingInviteeDto> Invitees { get; set; } = new();
}
