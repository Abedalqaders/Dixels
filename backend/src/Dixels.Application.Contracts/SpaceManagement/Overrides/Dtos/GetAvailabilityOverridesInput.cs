using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class GetAvailabilityOverridesInput : PagedResultRequestDto
{
    public OverrideScope Scope { get; set; }

    public Guid ScopeId { get; set; }

    /// <summary>
    /// False (the default): only closures still to come or happening now, soonest first.
    /// True: every one, most recent first — past ones pile up for years.
    /// </summary>
    public bool IncludePast { get; set; }
}
