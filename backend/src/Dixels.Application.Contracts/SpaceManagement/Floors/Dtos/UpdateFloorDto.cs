using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>Identity fields only — see <see cref="UpdateFloorConstraintsDto"/> for the
/// atomic, concurrency-checked constraints save.</summary>
public class UpdateFloorDto
{
    /// <summary>Every name it should have, one per language — a language left out loses its name. The default language's is required.</summary>
    [Required]
    [MinLength(1)]
    public List<LocalizedNameDto> Names { get; set; } = [];

    public int? FloorNumber { get; set; }
}
