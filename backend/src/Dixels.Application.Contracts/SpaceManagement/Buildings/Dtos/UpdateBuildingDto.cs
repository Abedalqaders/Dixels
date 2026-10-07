using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>Identity fields only — Days/Hours/duration/horizon/lead-time go through
/// <see cref="UpdateBuildingConstraintsDto"/> instead, since those need the atomic,
/// concurrency-checked save path.</summary>
public class UpdateBuildingDto
{
    /// <summary>Every name it should have, one per language — a language left out loses its name. The default language's is required.</summary>
    [Required]
    [MinLength(1)]
    public List<LocalizedNameDto> Names { get; set; } = [];

    [StringLength(BuildingConsts.MaxBuildingNumberLength)]
    public string? BuildingNumber { get; set; }

    /// <summary>
    /// Left out (null): the addresses stay as they are. Given: exactly these, one per language
    /// it has a name in — a language left out loses its address.
    /// </summary>
    public List<BuildingAddressDto>? Addresses { get; set; }

    [Required]
    [StringLength(BuildingConsts.MaxTimezoneLength)]
    public string Timezone { get; set; } = string.Empty;
}
