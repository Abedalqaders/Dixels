using System;
using System.Threading.Tasks;
using Dixels.Bookings;
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
    public virtual Task<BuildingDto> GetAsync(Guid id) => _buildingsAppService.GetAsync(id);

    [HttpGet]
    public virtual Task<PagedResultDto<BuildingDto>> GetListAsync([FromQuery] GetBuildingsInput input) =>
        _buildingsAppService.GetListAsync(input);

    [HttpPost]
    public virtual Task<BuildingDto> CreateAsync([FromBody] CreateBuildingDto input) =>
        _buildingsAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual Task<BuildingDto> UpdateAsync(Guid id, [FromBody] UpdateBuildingDto input) =>
        _buildingsAppService.UpdateAsync(id, input);

    [HttpPost("{id}/constraints/impact")]
    public virtual Task<BookingImpactDto> GetConstraintsImpactAsync(Guid id, [FromBody] UpdateBuildingConstraintsDto input) =>
        _buildingsAppService.GetConstraintsImpactAsync(id, input);

    [HttpGet("{id}/delete-impact")]
    public virtual Task<BookingImpactDto> GetDeleteImpactAsync(Guid id) => _buildingsAppService.GetDeleteImpactAsync(id);

    [HttpPut("{id}/constraints")]
    public virtual Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, [FromBody] UpdateBuildingConstraintsDto input) =>
        _buildingsAppService.UpdateConstraintsAsync(id, input);

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id) => _buildingsAppService.DeleteAsync(id);

    [HttpPost("{id}/restore")]
    public virtual Task RestoreAsync(Guid id) => _buildingsAppService.RestoreAsync(id);
}
