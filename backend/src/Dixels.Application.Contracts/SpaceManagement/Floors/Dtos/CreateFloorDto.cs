using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

public class CreateFloorDto
{
    [Required]
    public Guid BuildingId { get; set; }

    /// <summary>One per language; the default language's is required.</summary>
    [Required]
    [MinLength(1)]
    public List<LocalizedNameDto> Names { get; set; } = [];

    public int? FloorNumber { get; set; }
}
