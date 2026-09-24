using System;
using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

public class CreateBuildingDto
{
    [Required]
    [StringLength(BuildingConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

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
    public int MinLeadMinutes { get; set; }
}
