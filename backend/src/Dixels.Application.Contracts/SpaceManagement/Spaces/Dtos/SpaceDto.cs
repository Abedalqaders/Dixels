using System;
using System.Collections.Generic;
using Dixels.Localization;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class SpaceDto : EntityDto<Guid>
{
    public Guid FloorId { get; set; }
    /// <summary>
    /// The name to show: in the request's language (Accept-Language), else the default
    /// language's.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Every name it has, one per language — what the edit form shows.</summary>
    public List<LocalizedNameDto> Names { get; set; } = [];

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
