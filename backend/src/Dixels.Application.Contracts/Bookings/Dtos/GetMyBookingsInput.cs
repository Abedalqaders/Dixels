using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Timing;

namespace Dixels.Bookings;

/// <summary>
/// A range of days on the employee's building's calendar: <c>From</c> is the first day,
/// <c>To</c> the day after the last (exclusive), both local dates with no time — the server
/// turns them into instants in the building's timezone, so a week is a building week.
/// </summary>
public class GetMyBookingsInput
{
    /// <summary>The longest range one request may ask for — a 6-week month grid, with room to spare.</summary>
    public const int MaxDays = 62;

    [Required]
    [DisableDateTimeNormalization]
    public DateTime From { get; set; }

    [Required]
    [DisableDateTimeNormalization]
    public DateTime To { get; set; }
}
