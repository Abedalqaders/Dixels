using System;
using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

public class CreateAvailabilityOverrideDto
{
    [Required]
    public OverrideScope Scope { get; set; }

    [Required]
    public Guid ScopeId { get; set; }

    [Required]
    public DateTimeOffset StartsAt { get; set; }

    [Required]
    public DateTimeOffset EndsAt { get; set; }

    [Required]
    public OverrideEffect Effect { get; set; }

    [Required]
    public ReasonCategory ReasonCategory { get; set; }

    [StringLength(AvailabilityOverrideConsts.MaxReasonDetailLength)]
    public string? ReasonDetail { get; set; }
}
