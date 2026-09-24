using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceManagement;

public interface IFloorsAppService : IApplicationService
{
    Task<FloorDto> GetAsync(Guid id);

    /// <summary>Paged, searchable (by name), optionally including soft-deleted rows — backs
    /// the admin Floors list page (scoped to one Building).</summary>
    Task<PagedResultDto<FloorDto>> GetListAsync(GetFloorsInput input);

    Task<FloorDto> CreateAsync(CreateFloorDto input);

    Task<FloorDto> UpdateAsync(Guid id, UpdateFloorDto input);

    Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateFloorConstraintsDto input);

    /// <summary>Resolved values (space→floor→building) plus the Building ancestor trail, in
    /// one round trip — backs the constraints page's "People will see" summary card.</summary>
    Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id);

    /// <summary>Soft-deletes the floor and cascades to its Spaces, sharing one
    /// <c>DeletionBatchId</c> so a later restore is scoped to exactly this operation.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>Restores the floor and only the Spaces soft-deleted in the same batch.</summary>
    Task RestoreAsync(Guid id);
}
