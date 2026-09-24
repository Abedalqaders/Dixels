using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceManagement;

public interface ISpacesAppService : IApplicationService
{
    Task<SpaceDto> GetAsync(Guid id);

    /// <summary>Paged, searchable (by name), optionally filtered by space type and/or
    /// including soft-deleted rows — backs the admin Spaces list page (scoped to one
    /// Floor).</summary>
    Task<PagedResultDto<SpaceDto>> GetListAsync(GetSpacesInput input);

    Task<SpaceDto> CreateAsync(CreateSpaceDto input);

    Task<SpaceDto> UpdateAsync(Guid id, UpdateSpaceDto input);

    Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateSpaceConstraintsDto input);

    /// <summary>Resolved values (space→floor→building) plus the Building+Floor ancestor
    /// trail, in one round trip.</summary>
    Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id);

    Task DeleteAsync(Guid id);

    Task RestoreAsync(Guid id);
}
