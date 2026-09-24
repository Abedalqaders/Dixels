using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

public class CreateSpaceTypeDto
{
    [Required]
    [StringLength(SpaceTypeConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    public IconKey IconKey { get; set; } = IconKey.Generic;
}
