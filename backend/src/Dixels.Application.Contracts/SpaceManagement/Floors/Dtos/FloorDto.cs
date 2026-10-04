using System;
using System.Collections.Generic;
using Dixels.Localization;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class FloorDto : EntityDto<Guid>
{
    public Guid BuildingId { get; set; }
    /// <summary>
    /// The name to show: in the request's language (Accept-Language), else the default
    /// language's.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Every name it has, one per language — what the edit form shows.</summary>
    public List<LocalizedNameDto> Names { get; set; } = [];

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
