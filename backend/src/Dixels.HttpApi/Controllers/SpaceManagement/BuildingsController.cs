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
[Route("api/app/buildings")]
public class BuildingsController : DixelsController, IBuildingsAppService
{
    private readonly IBuildingsAppService _buildingsAppService;

    public BuildingsController(IBuildingsAppService buildingsAppService)
    {
        _buildingsAppService = buildingsAppService;
    }

    [HttpGet("{id}")]
    public virtual async Task<BuildingDto> GetAsync(Guid id) => await _buildingsAppService.GetAsync(id);

    [HttpGet]
    public virtual async Task<PagedResultDto<BuildingDto>> GetListAsync([FromQuery] GetBuildingsInput input) =>
        await _buildingsAppService.GetListAsync(input);

    [HttpPost]
    public virtual async Task<BuildingDto> CreateAsync([FromBody] CreateBuildingDto input) =>
        await _buildingsAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual async Task<BuildingDto> UpdateAsync(Guid id, [FromBody] UpdateBuildingDto input) =>
        await _buildingsAppService.UpdateAsync(id, input);

    [HttpPost("{id}/constraints/impact")]
    public virtual async Task<ReservationImpactDto> GetConstraintsImpactAsync(Guid id, [FromBody] UpdateBuildingConstraintsDto input, [FromQuery] int skip = 0) =>
        await _buildingsAppService.GetConstraintsImpactAsync(id, input, skip);

    [HttpGet("{id}/delete-impact")]
    public virtual async Task<ReservationImpactDto> GetDeleteImpactAsync(Guid id, [FromQuery] int skip = 0) => await _buildingsAppService.GetDeleteImpactAsync(id, skip);

    [HttpPut("{id}/constraints")]
    public virtual async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, [FromBody] UpdateBuildingConstraintsDto input) =>
        await _buildingsAppService.UpdateConstraintsAsync(id, input);

    [HttpDelete("{id}")]
    public virtual async Task DeleteAsync(Guid id) => await _buildingsAppService.DeleteAsync(id);

    [HttpPost("{id}/restore")]
    public virtual async Task RestoreAsync(Guid id) => await _buildingsAppService.RestoreAsync(id);
}
