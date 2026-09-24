using System;

namespace Dixels.SpaceManagement;

/// <summary>
/// The constraint values a booker actually experiences at a Floor or Space, resolved
/// per-field (space wins, then floor, then building), plus the ancestor trail the frontend
/// needs for its breadcrumb in one round trip. Shared shape for both levels: a Floor's own
/// resolved page leaves <see cref="FloorId"/>/<see cref="FloorName"/> unset (it's not its
/// own ancestor); a Space's fills in both.
/// </summary>
public class ResolvedConstraintsDto
{
    public string Timezone { get; set; } = string.Empty;
    public FieldValueDto<int[]> Days { get; set; } = new();
    public FieldValueDto<OperatingWindowDto> Hours { get; set; } = new();
    public FieldValueDto<int> MaxDurationMinutes { get; set; } = new();
    public int MaxHorizonDays { get; set; }
    public int MinLeadMinutes { get; set; }
    public int? MinAttendees { get; set; }
    public int? Capacity { get; set; }

    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public Guid? FloorId { get; set; }
    public string? FloorName { get; set; }
}
