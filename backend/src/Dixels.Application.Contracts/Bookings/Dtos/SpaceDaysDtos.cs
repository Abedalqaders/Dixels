using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Bookings;

/// <summary>
/// Days of one space's calendar, building-local dates, both inclusive. The server cuts the
/// range to today … the last date the building lets you book.
/// </summary>
public class GetSpaceDaysInput
{
    [Required]
    public DateOnly From { get; set; }

    [Required]
    public DateOnly To { get; set; }
}

/// <summary>A space's bookable days — what the booking form offers dates and times from.</summary>
public class SpaceDaysDto
{
    public List<SpaceDayDto> Days { get; set; } = new();
}

/// <summary>
/// One day of a space, in minutes from local midnight: when it's open, when an admin closed
/// it, and when it's booked (only whether a booking is yours, never whose).
/// </summary>
public class SpaceDayDto
{
    public DateOnly Date { get; set; }

    public List<DayRangeDto> Open { get; set; } = new();

    public List<DayRangeDto> Closed { get; set; } = new();

    public List<DayRangeDto> Busy { get; set; } = new();
}
