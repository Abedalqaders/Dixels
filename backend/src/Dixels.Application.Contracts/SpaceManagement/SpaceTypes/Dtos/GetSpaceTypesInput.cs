using Volo.Abp.Application.Dtos;

namespace Dixels.SpaceManagement;

public class GetSpaceTypesInput : PagedResultRequestDto
{
    /// <summary>Name search — matches a name in any language, anywhere in it, ignoring case.</summary>
    public string? Filter { get; set; }
}
