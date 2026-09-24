using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class SpaceTypeDto : EntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public IconKey IconKey { get; set; }
}
