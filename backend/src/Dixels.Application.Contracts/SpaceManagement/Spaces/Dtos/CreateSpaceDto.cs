using System;
using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

public class CreateSpaceDto
{
    [Required]
    public Guid FloorId { get; set; }

    [Required]
    [StringLength(SpaceConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid SpaceTypeId { get; set; }

    [Required]
    public int Capacity { get; set; }
}
