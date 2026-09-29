using System;
using System.Collections.Generic;
using Volo.Abp.Timing;

namespace Dixels.Bookings;

/// <summary>One upcoming booking an admin's change would affect, as the admin sees it.</summary>
public class AffectedBookingDto
{
    public Guid BookingId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Who booked it (name, or user name when there's no name).</summary>
    public string BookedBy { get; set; } = string.Empty;

    public string SpaceName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;

    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }

    /// <summary>Why it no longer fits, in a few words each ("Open 09:00–17:00 only").</summary>
    public List<string> Reasons { get; set; } = new();
}

/// <summary>"This change affects N upcoming bookings" — shown before an admin saves, closes or deletes.</summary>
public class BookingImpactDto
{
    public int Count { get; set; }
    public List<AffectedBookingDto> Bookings { get; set; } = new();

    /// <summary>Deleting a building: how many employees are assigned to it (they can't book until reassigned).</summary>
    public int AssignedEmployees { get; set; }
}
