using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceManagement;

public interface ISpaceTypesAppService : IApplicationService
{
    Task<ListResultDto<SpaceTypeDto>> GetListAsync();

    Task<SpaceTypeDto> CreateAsync(CreateSpaceTypeDto input);

    Task<SpaceTypeDto> UpdateAsync(Guid id, UpdateSpaceTypeDto input);

    Task DeleteAsync(Guid id);
}
