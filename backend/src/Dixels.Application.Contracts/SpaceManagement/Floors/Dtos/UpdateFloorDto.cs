using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

/// <summary>Identity fields only — see <see cref="UpdateFloorConstraintsDto"/> for the
/// atomic, concurrency-checked constraints save.</summary>
public class UpdateFloorDto
{
    [Required]
    [StringLength(FloorConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    public int? FloorNumber { get; set; }
}
