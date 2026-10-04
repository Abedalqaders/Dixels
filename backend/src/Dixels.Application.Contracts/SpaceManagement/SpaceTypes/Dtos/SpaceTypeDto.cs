using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class SpaceTypeDto : EntityDto<Guid>
{
    /// <summary>
    /// The name to show: in the request's language (Accept-Language), else the default
    /// language's.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public IconKey IconKey { get; set; }

    /// <summary>Every name it has, one per language — what the edit form shows.</summary>
    public List<SpaceTypeNameDto> Names { get; set; } = [];
}
