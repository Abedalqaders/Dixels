using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Controllers.SpaceManagement;

[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/spaces")]
public class SpacesController : DixelsController, ISpacesAppService
{
    private readonly ISpacesAppService _spacesAppService;

    public SpacesController(ISpacesAppService spacesAppService)
    {
        _spacesAppService = spacesAppService;
    }

    [HttpGet("{id}")]
    public virtual async Task<SpaceDto> GetAsync(Guid id) => await _spacesAppService.GetAsync(id);

    [HttpGet]
    public virtual async Task<PagedResultDto<SpaceDto>> GetListAsync([FromQuery] GetSpacesInput input) =>
        await _spacesAppService.GetListAsync(input);

    [HttpPost]
    public virtual async Task<SpaceDto> CreateAsync([FromBody] CreateSpaceDto input) => await _spacesAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual async Task<SpaceDto> UpdateAsync(Guid id, [FromBody] UpdateSpaceDto input) =>
        await _spacesAppService.UpdateAsync(id, input);

    [HttpPost("{id}/constraints/impact")]
    public virtual async Task<ReservationImpactDto> GetConstraintsImpactAsync(Guid id, [FromBody] UpdateSpaceConstraintsDto input, [FromQuery] int skip = 0) =>
        await _spacesAppService.GetConstraintsImpactAsync(id, input, skip);

    [HttpPost("{id}/impact")]
    public virtual async Task<ReservationImpactDto> GetUpdateImpactAsync(Guid id, [FromBody] UpdateSpaceDto input, [FromQuery] int skip = 0) =>
        await _spacesAppService.GetUpdateImpactAsync(id, input, skip);

    [HttpGet("{id}/delete-impact")]
    public virtual async Task<ReservationImpactDto> GetDeleteImpactAsync(Guid id, [FromQuery] int skip = 0) => await _spacesAppService.GetDeleteImpactAsync(id, skip);

    [HttpPut("{id}/constraints")]
    public virtual async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, [FromBody] UpdateSpaceConstraintsDto input) =>
        await _spacesAppService.UpdateConstraintsAsync(id, input);

    [HttpGet("{id}/resolved-constraints")]
    public virtual async Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id) =>
        await _spacesAppService.GetResolvedConstraintsAsync(id);

    [HttpDelete("{id}")]
    public virtual async Task DeleteAsync(Guid id) => await _spacesAppService.DeleteAsync(id);

    [HttpPost("{id}/restore")]
    public virtual async Task RestoreAsync(Guid id) => await _spacesAppService.RestoreAsync(id);
}
