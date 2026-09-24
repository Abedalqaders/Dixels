using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class AvailabilityOverrideDto : EntityDto<Guid>
{
    public OverrideScope Scope { get; set; }
    public Guid ScopeId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public OverrideEffect Effect { get; set; }
    public ReasonCategory ReasonCategory { get; set; }
    public string? ReasonDetail { get; set; }
}
