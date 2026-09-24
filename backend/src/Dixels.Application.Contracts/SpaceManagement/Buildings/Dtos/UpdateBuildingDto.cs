using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

/// <summary>Identity fields only — Days/Hours/duration/horizon/lead-time go through
/// <see cref="UpdateBuildingConstraintsDto"/> instead, since those need the atomic,
/// concurrency-checked save path.</summary>
public class UpdateBuildingDto
{
    [Required]
    [StringLength(BuildingConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(BuildingConsts.MaxBuildingNumberLength)]
    public string? BuildingNumber { get; set; }

    [Required]
    [StringLength(BuildingConsts.MaxTimezoneLength)]
    public string Timezone { get; set; } = string.Empty;
}
