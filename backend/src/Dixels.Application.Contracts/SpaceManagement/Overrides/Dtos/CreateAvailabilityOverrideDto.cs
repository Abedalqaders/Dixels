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

    /// <summary>
    /// Also cancel the upcoming bookings this closure would fall on (see the
    /// closure impact endpoint). Left false, they stay — grandfathered under the rules
    /// they were booked with.
    /// </summary>
    public bool CancelAffectedBookings { get; set; }
}
