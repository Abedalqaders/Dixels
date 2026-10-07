using System.ComponentModel.DataAnnotations;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>A building's street address in one language, shown to guests (invite emails, the .ics location).</summary>
public class BuildingAddressDto
{
    /// <summary>ABP culture name; one of the languages the building has a name in (others are dropped).</summary>
    [Required]
    [StringLength(LocalizedNameConsts.MaxLanguageLength)]
    public string Language { get; set; } = string.Empty;

    [Required]
    [StringLength(BuildingConsts.MaxAddressLength)]
    public string Address { get; set; } = string.Empty;
}
