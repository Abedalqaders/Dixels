using System;
using Dixels.SpaceManagement;
using System.Collections.Generic;

namespace Dixels.Bookings;

/// <summary>
/// Everything the "Find a space" page needs about the one building the current employee
/// can book in: the building-wide rules, and every space grouped by floor with its own
/// resolved limits (and which level set each one).
/// </summary>
public class BookableBuildingDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public int MaxHorizonDays { get; set; }
    public int MinLeadMinutes { get; set; }

    /// <summary>Whether one person may hold two bookings at once here.</summary>
    public OwnOverlapPolicy OwnOverlapPolicy { get; set; }

    /// <summary>The booking time grid (install config) — the UI offers start/end times on it.</summary>
    public int SlotMinutes { get; set; }

    public List<BookableFloorDto> Floors { get; set; } = new();
}
