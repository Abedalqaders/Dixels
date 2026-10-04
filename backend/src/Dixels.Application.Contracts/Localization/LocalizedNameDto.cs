using System.ComponentModel.DataAnnotations;

namespace Dixels.Localization;

/// <summary>A name in one language, e.g. { language: "ar", name: "غرفة اجتماعات" } — for space types, buildings, floors and spaces.</summary>
public class LocalizedNameDto
{
    /// <summary>ABP culture name, one of the app's languages ("en", "ar").</summary>
    [Required]
    [StringLength(LocalizedNameConsts.MaxLanguageLength)]
    public string Language { get; set; } = string.Empty;

    [Required]
    [StringLength(LocalizedNameConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;
}
