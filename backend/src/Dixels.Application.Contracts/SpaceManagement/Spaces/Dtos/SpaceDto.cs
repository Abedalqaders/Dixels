using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class SpaceDto : EntityDto<Guid>
{
    public Guid FloorId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Set by every <c>GetListAsync</c> result (the standalone Spaces page needs
    /// these to show which floor/building a row belongs to) — never populated by
    /// <c>GetAsync</c>.</summary>
    public string? FloorName { get; set; }
    public string? BuildingName { get; set; }

    public Guid SpaceTypeId { get; set; }
    public int Capacity { get; set; }

    /// <summary>Null means "inherit from the resolved Floor value."</summary>
    public int[]? Days { get; set; }

    public OperatingWindowDto? Hours { get; set; }
    public int? MaxDurationMinutes { get; set; }
    public int? MinAttendees { get; set; }

    public bool HasOverrides { get; set; }

    public bool IsDeleted { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
