using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Timing;

namespace Dixels.Bookings;

public class BookingDto : EntityDto<Guid>
{
    public Guid SpaceId { get; set; }
    public string SpaceName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;

    /// <summary>UTC instants — the stored truth.</summary>
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }

    /// <summary>The same instants on the building's wall clock, for display (not normalized to UTC).</summary>
    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }

    public int Attendees { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>Set for a booking that's one date of a recurring series, with how the series repeats.</summary>
    public Guid? SeriesId { get; set; }

    /// <summary>For a booking an admin cancelled (a rule change, a closure, a removed room): shown struck through, with why.</summary>
    public bool CancelledByAdmin { get; set; }
    public string? CancelReason { get; set; }
    public RecurrenceDto? Recurrence { get; set; }

    public List<BookingInviteeDto> Invitees { get; set; } = new();

    /// <summary>False when I'm only invited: then it's read-only for me (no cancel, no editing guests).</summary>
    public bool IsOwner { get; set; }

    /// <summary>Who booked it, for "Invited by …".</summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>The room's seats and minimum head count now (null = no limit), for checking an edit of the guests before saving.</summary>
    public int? Capacity { get; set; }
    public int? MinAttendees { get; set; }
}
