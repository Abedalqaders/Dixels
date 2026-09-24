using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class GetFloorsInput : PagedAndSortedResultRequestDto
{
    /// <summary>Optional — omit to list floors across every building (the standalone Floors
    /// page); set to scope to one building (the drill-down Floors-of-a-building page).</summary>
    public Guid? BuildingId { get; set; }

    /// <summary>Name search — matches anywhere in the name, case-insensitive.</summary>
    public string? Filter { get; set; }

    public bool IncludeDeleted { get; set; }
}
