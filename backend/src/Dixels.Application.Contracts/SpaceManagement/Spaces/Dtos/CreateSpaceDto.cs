using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

public class CreateSpaceDto
{
    [Required]
    public Guid FloorId { get; set; }

    /// <summary>One per language; the default language's is required.</summary>
    [Required]
    [MinLength(1)]
    public List<LocalizedNameDto> Names { get; set; } = [];

    [Required]
    public Guid SpaceTypeId { get; set; }

    [Required]
    public int Capacity { get; set; }

    /// <summary>
    /// Optional: one per opening of the add dialog. Sending the same request again with it
    /// (a double-click, a retry) returns the first result instead of a second copy.
    /// </summary>
    [StringLength(HierarchyConsts.MaxIdempotencyKeyLength)]
    public string? IdempotencyKey { get; set; }
}
