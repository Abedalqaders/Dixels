using System;
using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

/// <summary>Identity/physical fields — Days/Hours/MaxDurationMinutes/MinAttendees go
/// through <see cref="UpdateSpaceConstraintsDto"/> instead, since those need the atomic,
/// concurrency-checked save path.</summary>
public class UpdateSpaceDto
{
    [Required]
    [StringLength(SpaceConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid SpaceTypeId { get; set; }

    [Required]
    public int Capacity { get; set; }
}
