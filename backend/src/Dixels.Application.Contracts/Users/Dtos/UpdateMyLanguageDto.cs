using System.ComponentModel.DataAnnotations;

namespace Dixels.Users;

public class UpdateMyLanguageDto
{
    /// <summary>One of the app's languages, by culture name ("en", "ar").</summary>
    [Required]
    [StringLength(16)]
    public string Language { get; set; } = string.Empty;
}
