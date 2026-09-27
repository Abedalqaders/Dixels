using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class GetFloorsInput : PagedAndSortedResultRequestDto
{
    /// <summary>Optional — omit to list floors across every building (the standalone Floors
    /// page); set to scope to one building (the drill-down Floors-of-a-building page).</summary>
    public Guid? BuildingId { get; set; }

    /// <summary>Name search — matches anywhere in the floor's name or its Building's name,
    /// case-insensitive.</summary>
    public string? Filter { get; set; }

    /// <summary>Restricts <see cref="Filter"/> to the floor's own name. The admin explorer tree
    /// sets it: it already lists matching buildings separately, so floors matched only
    /// through their building name would just repeat those and crowd out real hits.</summary>
    public bool FloorNameOnly { get; set; }

    public bool IncludeDeleted { get; set; }
}
