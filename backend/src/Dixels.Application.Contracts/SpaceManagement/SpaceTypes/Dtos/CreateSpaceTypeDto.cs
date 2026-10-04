using Dixels.Localization;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

public class CreateSpaceTypeDto
{
    /// <summary>One per language; the default language's is required.</summary>
    [Required]
    [MinLength(1)]
    public List<LocalizedNameDto> Names { get; set; } = [];

    public IconKey IconKey { get; set; } = IconKey.Generic;
}
