using Dixels.Localization;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

public class UpdateSpaceTypeDto
{
    /// <summary>
    /// Every name the type should have, one per language — a language left out loses its
    /// name. The default language's is required.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<LocalizedNameDto> Names { get; set; } = [];

    public IconKey IconKey { get; set; }
}
