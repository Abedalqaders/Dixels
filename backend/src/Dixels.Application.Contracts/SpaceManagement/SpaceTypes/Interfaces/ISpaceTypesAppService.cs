using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceManagement;

public interface ISpaceTypesAppService : IApplicationService
{
    Task<PagedResultDto<SpaceTypeDto>> GetListAsync(GetSpaceTypesInput input);

    Task<SpaceTypeDto> CreateAsync(CreateSpaceTypeDto input);

    Task<SpaceTypeDto> UpdateAsync(Guid id, UpdateSpaceTypeDto input);

    Task DeleteAsync(Guid id);
}
