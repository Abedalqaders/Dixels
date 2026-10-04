using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

public class CreateBuildingDto
{
    /// <summary>One per language; the default language's is required.</summary>
    [Required]
    [MinLength(1)]
    public List<LocalizedNameDto> Names { get; set; } = [];

    [StringLength(BuildingConsts.MaxBuildingNumberLength)]
    public string? BuildingNumber { get; set; }

    [Required]
    [StringLength(BuildingConsts.MaxTimezoneLength)]
    public string Timezone { get; set; } = string.Empty;

    [Required]
    public int[] Days { get; set; } = Array.Empty<int>();

    [Required]
    public OperatingWindowDto Hours { get; set; } = new();

    public int MaxDurationMinutes { get; set; }
    public int MaxHorizonDays { get; set; }

    /// <summary>How far ahead recurring bookings may run (at least <see cref="MaxHorizonDays"/>). Left out: unchanged, or 90 for a new building.</summary>
    public int? MaxSeriesHorizonDays { get; set; }
    public int MinLeadMinutes { get; set; }

    /// <summary>Whether one person may hold two bookings at once in this building. Warn when left out.</summary>
    public OwnOverlapPolicy OwnOverlapPolicy { get; set; } = OwnOverlapPolicy.Warn;
}
