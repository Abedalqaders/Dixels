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
[Route("api/app/floors")]
public class FloorsController : DixelsController, IFloorsAppService
{
    private readonly IFloorsAppService _floorsAppService;

    public FloorsController(IFloorsAppService floorsAppService)
    {
        _floorsAppService = floorsAppService;
    }

    [HttpGet("{id}")]
    public virtual async Task<FloorDto> GetAsync(Guid id) => await _floorsAppService.GetAsync(id);

    [HttpGet]
    public virtual async Task<PagedResultDto<FloorDto>> GetListAsync([FromQuery] GetFloorsInput input) =>
        await _floorsAppService.GetListAsync(input);

    [HttpPost]
    public virtual async Task<FloorDto> CreateAsync([FromBody] CreateFloorDto input) => await _floorsAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual async Task<FloorDto> UpdateAsync(Guid id, [FromBody] UpdateFloorDto input) =>
        await _floorsAppService.UpdateAsync(id, input);

    [HttpPost("{id}/constraints/impact")]
    public virtual async Task<ReservationImpactDto> GetConstraintsImpactAsync(Guid id, [FromBody] UpdateFloorConstraintsDto input, [FromQuery] int skip = 0) =>
        await _floorsAppService.GetConstraintsImpactAsync(id, input, skip);

    [HttpGet("{id}/delete-impact")]
    public virtual async Task<ReservationImpactDto> GetDeleteImpactAsync(Guid id, [FromQuery] int skip = 0) => await _floorsAppService.GetDeleteImpactAsync(id, skip);

    [HttpPut("{id}/constraints")]
    public virtual async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, [FromBody] UpdateFloorConstraintsDto input) =>
        await _floorsAppService.UpdateConstraintsAsync(id, input);

    [HttpGet("{id}/resolved-constraints")]
    public virtual async Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id) =>
        await _floorsAppService.GetResolvedConstraintsAsync(id);

    [HttpDelete("{id}")]
    public virtual async Task DeleteAsync(Guid id) => await _floorsAppService.DeleteAsync(id);

    [HttpPost("{id}/restore")]
    public virtual async Task RestoreAsync(Guid id) => await _floorsAppService.RestoreAsync(id);
}
