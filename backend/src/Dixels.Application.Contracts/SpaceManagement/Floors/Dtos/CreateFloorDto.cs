using System;
using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

public class CreateFloorDto
{
    [Required]
    public Guid BuildingId { get; set; }

    [Required]
    [StringLength(FloorConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    public int? FloorNumber { get; set; }
}
