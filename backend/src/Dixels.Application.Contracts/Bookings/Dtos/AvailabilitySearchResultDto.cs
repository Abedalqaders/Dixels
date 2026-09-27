using System;
using System.Collections.Generic;
using Volo.Abp.Timing;

namespace Dixels.Bookings;

public class AvailabilitySearchResultDto
{
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;

    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }

    /// <summary>Available spaces first (floor, then name), then the unavailable ones.</summary>
    public List<SpaceAvailabilityDto> Spaces { get; set; } = new();
}
