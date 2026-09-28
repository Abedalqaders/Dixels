using System;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Controllers.SpaceManagement;

[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/space-types")]
public class SpaceTypesController : DixelsController, ISpaceTypesAppService
{
    private readonly ISpaceTypesAppService _spaceTypesAppService;

    public SpaceTypesController(ISpaceTypesAppService spaceTypesAppService)
    {
        _spaceTypesAppService = spaceTypesAppService;
    }

    [HttpGet]
    public virtual Task<ListResultDto<SpaceTypeDto>> GetListAsync() => _spaceTypesAppService.GetListAsync();

    [HttpPost]
    public virtual Task<SpaceTypeDto> CreateAsync([FromBody] CreateSpaceTypeDto input) =>
        _spaceTypesAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual Task<SpaceTypeDto> UpdateAsync(Guid id, [FromBody] UpdateSpaceTypeDto input) =>
        _spaceTypesAppService.UpdateAsync(id, input);

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id) => _spaceTypesAppService.DeleteAsync(id);
}
