using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class GetSpacesInput : PagedAndSortedResultRequestDto
{
    /// <summary>Optional — omit to list spaces across every floor (the standalone Spaces
    /// page); set to scope to one floor (the drill-down Spaces-of-a-floor page).</summary>
    public Guid? FloorId { get; set; }

    /// <summary>Optional building-level filter for the standalone Spaces page — symmetric
    /// with Floors' own BuildingId filter, cheap to support since the query already joins
    /// through to Building for BuildingName.</summary>
    public Guid? BuildingId { get; set; }

    /// <summary>Name search — matches anywhere in the space's own name, its Floor's name, or
    /// its Building's name, case-insensitive.</summary>
    public string? Filter { get; set; }

    /// <summary>Optional space-type filter, for the Spaces list page's type dropdown.</summary>
    public Guid? SpaceTypeId { get; set; }

    public bool IncludeDeleted { get; set; }
}
