using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class GetBuildingsInput : PagedAndSortedResultRequestDto
{
    /// <summary>Name search — matches anywhere in the name, case-insensitive.</summary>
    public string? Filter { get; set; }

    public bool IncludeDeleted { get; set; }
}
