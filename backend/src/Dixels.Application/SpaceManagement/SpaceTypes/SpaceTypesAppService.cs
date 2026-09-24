using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.SpaceManagement;

[Authorize(DixelsPermissions.SpaceTypes.Default)]
public class SpaceTypesAppService : DixelsAppService, ISpaceTypesAppService
{
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;
    private readonly SpaceTypeManager _spaceTypeManager;

    public SpaceTypesAppService(IRepository<SpaceType, Guid> spaceTypeRepository, SpaceTypeManager spaceTypeManager)
    {
        _spaceTypeRepository = spaceTypeRepository;
        _spaceTypeManager = spaceTypeManager;
    }

    public async Task<ListResultDto<SpaceTypeDto>> GetListAsync()
    {
        var spaceTypes = await _spaceTypeRepository.GetListAsync();

        return new ListResultDto<SpaceTypeDto>(
            spaceTypes.OrderBy(t => t.Name).Select(ObjectMapper.Map<SpaceType, SpaceTypeDto>).ToList());
    }

    [Authorize(DixelsPermissions.SpaceTypes.Create)]
    public async Task<SpaceTypeDto> CreateAsync(CreateSpaceTypeDto input)
    {
        var spaceType = await _spaceTypeManager.CreateAsync(input.Name, input.IconKey);
        await _spaceTypeRepository.InsertAsync(spaceType);

        return ObjectMapper.Map<SpaceType, SpaceTypeDto>(spaceType);
    }

    [Authorize(DixelsPermissions.SpaceTypes.Edit)]
    public async Task<SpaceTypeDto> UpdateAsync(Guid id, UpdateSpaceTypeDto input)
    {
        var spaceType = await _spaceTypeRepository.GetAsync(id);

        await _spaceTypeManager.RenameAsync(spaceType, input.Name);
        spaceType.SetIconKey(input.IconKey);

        await _spaceTypeRepository.UpdateAsync(spaceType);

        return ObjectMapper.Map<SpaceType, SpaceTypeDto>(spaceType);
    }

    [Authorize(DixelsPermissions.SpaceTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _spaceTypeManager.EnsureNotInUseAsync(id);
        await _spaceTypeRepository.DeleteAsync(id);
    }
}
