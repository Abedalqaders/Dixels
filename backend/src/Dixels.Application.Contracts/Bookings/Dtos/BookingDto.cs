using System;
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
}
