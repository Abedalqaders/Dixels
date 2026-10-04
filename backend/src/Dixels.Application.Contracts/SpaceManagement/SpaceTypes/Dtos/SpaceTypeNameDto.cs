using System.ComponentModel.DataAnnotations;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>A space type's name in one language, e.g. { language: "ar", name: "غرفة اجتماعات" }.</summary>
public class SpaceTypeNameDto
{
    /// <summary>ABP culture name, one of the app's languages ("en", "ar").</summary>
    [Required]
    [StringLength(LocalizedNameConsts.MaxLanguageLength)]
    public string Language { get; set; } = string.Empty;

    [Required]
    [StringLength(SpaceTypeConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;
}
