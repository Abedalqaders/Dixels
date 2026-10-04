using System;
using System.Collections.Generic;
using Dixels.Localization;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class BuildingDto : EntityDto<Guid>
{
    /// <summary>
    /// The name to show: in the request's language (Accept-Language), else the default
    /// language's.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Every name it has, one per language — what the edit form shows.</summary>
    public List<LocalizedNameDto> Names { get; set; } = [];
    public string? BuildingNumber { get; set; }
    public string Timezone { get; set; } = string.Empty;

    /// <summary>Enabled days as <see cref="DayOfWeek"/> integers (0 = Sunday … 6 = Saturday).</summary>
    public int[] Days { get; set; } = Array.Empty<int>();

    public OperatingWindowDto Hours { get; set; } = new();

    public int MaxDurationMinutes { get; set; }
    public int MaxHorizonDays { get; set; }
    public int MaxSeriesHorizonDays { get; set; }
    public int MinLeadMinutes { get; set; }
    public OwnOverlapPolicy OwnOverlapPolicy { get; set; }

    public bool IsDeleted { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
