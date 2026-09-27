using System;
using System.Collections.Generic;

namespace Dixels.Bookings;

public class SpaceAvailabilityDto
{
    public BookableSpaceDto Space { get; set; } = new();
    public Guid FloorId { get; set; }
    public string FloorName { get; set; } = string.Empty;

    public bool IsAvailable { get; set; }

    /// <summary>Why it can't be booked for the window, most fundamental first. Empty when available.</summary>
    public List<BookingViolationDto> Violations { get; set; } = new();

    /// <summary>When available: "HH:mm" the free stretch runs until ("24:00" = midnight).</summary>
    public string? FreeUntil { get; set; }

    /// <summary>When unavailable only because of the time: the next "HH:mm" start that day where the same length fits.</summary>
    public string? NextFreeStart { get; set; }

    public List<DayRangeDto> Open { get; set; } = new();
    public List<DayRangeDto> Closed { get; set; } = new();
    public List<DayRangeDto> Busy { get; set; } = new();
}
