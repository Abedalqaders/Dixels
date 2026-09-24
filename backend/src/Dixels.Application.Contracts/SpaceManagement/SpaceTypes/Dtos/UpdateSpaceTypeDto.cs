using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

public class UpdateSpaceTypeDto
{
    [Required]
    [StringLength(SpaceTypeConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    public IconKey IconKey { get; set; }
}
