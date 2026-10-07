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
    public virtual Task<FloorDto> GetAsync(Guid id) => _floorsAppService.GetAsync(id);

    [HttpGet]
    public virtual Task<PagedResultDto<FloorDto>> GetListAsync([FromQuery] GetFloorsInput input) =>
        _floorsAppService.GetListAsync(input);

    [HttpPost]
    public virtual Task<FloorDto> CreateAsync([FromBody] CreateFloorDto input) => _floorsAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual Task<FloorDto> UpdateAsync(Guid id, [FromBody] UpdateFloorDto input) =>
        _floorsAppService.UpdateAsync(id, input);

    [HttpPost("{id}/constraints/impact")]
    public virtual Task<ReservationImpactDto> GetConstraintsImpactAsync(Guid id, [FromBody] UpdateFloorConstraintsDto input, [FromQuery] int skip = 0) =>
        _floorsAppService.GetConstraintsImpactAsync(id, input, skip);

    [HttpGet("{id}/delete-impact")]
    public virtual Task<ReservationImpactDto> GetDeleteImpactAsync(Guid id, [FromQuery] int skip = 0) => _floorsAppService.GetDeleteImpactAsync(id, skip);

    [HttpPut("{id}/constraints")]
    public virtual Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, [FromBody] UpdateFloorConstraintsDto input) =>
        _floorsAppService.UpdateConstraintsAsync(id, input);

    [HttpGet("{id}/resolved-constraints")]
    public virtual Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id) =>
        _floorsAppService.GetResolvedConstraintsAsync(id);

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id) => _floorsAppService.DeleteAsync(id);

    [HttpPost("{id}/restore")]
    public virtual Task RestoreAsync(Guid id) => _floorsAppService.RestoreAsync(id);
}
