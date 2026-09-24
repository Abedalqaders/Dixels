using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class BuildingDto : EntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? BuildingNumber { get; set; }
    public string Timezone { get; set; } = string.Empty;

    /// <summary>Enabled days as <see cref="DayOfWeek"/> integers (0 = Sunday … 6 = Saturday).</summary>
    public int[] Days { get; set; } = Array.Empty<int>();

    public OperatingWindowDto Hours { get; set; } = new();

    public int MaxDurationMinutes { get; set; }
    public int MaxHorizonDays { get; set; }
    public int MinLeadMinutes { get; set; }

    public bool IsDeleted { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
