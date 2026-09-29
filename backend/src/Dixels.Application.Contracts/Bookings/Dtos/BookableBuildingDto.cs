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

    /// <summary>
    /// The employee's building was deleted by an admin: name and timezone are still here (to
    /// explain, and to show past bookings on the right clock), but there's nothing to book.
    /// </summary>
    public bool IsRemoved { get; set; }
    public int MaxHorizonDays { get; set; }

    /// <summary>How far ahead a recurring booking's end date may be.</summary>
    public int MaxSeriesHorizonDays { get; set; }
    public int MinLeadMinutes { get; set; }

    /// <summary>Whether one person may hold two bookings at once here.</summary>
    public OwnOverlapPolicy OwnOverlapPolicy { get; set; }

    /// <summary>The booking time grid (install config) — the UI offers start/end times on it.</summary>
    public int SlotMinutes { get; set; }

    /// <summary>The building's own opening days (0 = Sunday … 6) and hours — what My calendar
    /// shades as closed. Rooms may be open for less; that's on each <see cref="BookableSpaceDto"/>.</summary>
    public int[] Days { get; set; } = Array.Empty<int>();
    public OperatingWindowDto Hours { get; set; } = new();

    public List<BookableFloorDto> Floors { get; set; } = new();
}
