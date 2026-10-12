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
    public virtual async Task<PagedResultDto<SpaceTypeDto>> GetListAsync([FromQuery] GetSpaceTypesInput input) =>
        await _spaceTypesAppService.GetListAsync(input);

    [HttpPost]
    public virtual async Task<SpaceTypeDto> CreateAsync([FromBody] CreateSpaceTypeDto input) =>
        await _spaceTypesAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual async Task<SpaceTypeDto> UpdateAsync(Guid id, [FromBody] UpdateSpaceTypeDto input) =>
        await _spaceTypesAppService.UpdateAsync(id, input);

    [HttpDelete("{id}")]
    public virtual async Task DeleteAsync(Guid id) => await _spaceTypesAppService.DeleteAsync(id);
}
