using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceManagement;

public interface IBuildingsAppService : IApplicationService
{
    Task<BuildingDto> GetAsync(Guid id);

    /// <summary>Paged, searchable (by name), optionally including soft-deleted rows — backs
    /// the admin Buildings list page.</summary>
    Task<PagedResultDto<BuildingDto>> GetListAsync(GetBuildingsInput input);

    Task<BuildingDto> CreateAsync(CreateBuildingDto input);

    Task<BuildingDto> UpdateAsync(Guid id, UpdateBuildingDto input);

    Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateBuildingConstraintsDto input);

    /// <summary>Soft-deletes the building and cascades to its Floors and Spaces, all sharing
    /// one <c>DeletionBatchId</c> so a later restore is scoped to exactly this operation.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>Restores the building and only the Floors/Spaces soft-deleted in the same
    /// batch — never a descendant that was independently deleted earlier.</summary>
    Task RestoreAsync(Guid id);
}
