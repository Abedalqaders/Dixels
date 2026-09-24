using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class FloorDto : EntityDto<Guid>
{
    public Guid BuildingId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Set by every <c>GetListAsync</c> result (the standalone Floors page needs it
    /// to show which building a row belongs to) — never populated by <c>GetAsync</c>.</summary>
    public string? BuildingName { get; set; }

    public int? FloorNumber { get; set; }

    /// <summary>Null means "inherit from the Building."</summary>
    public int[]? Days { get; set; }

    public OperatingWindowDto? Hours { get; set; }
    public int? MaxDurationMinutes { get; set; }

    /// <summary>True if any of the nullable override fields above is set — backs the
    /// hierarchy tree's "Custom" badge without the frontend needing to inspect each field.</summary>
    public bool HasOverrides { get; set; }

    public bool IsDeleted { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
