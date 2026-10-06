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
    public virtual Task<SpaceDto> GetAsync(Guid id) => _spacesAppService.GetAsync(id);

    [HttpGet]
    public virtual Task<PagedResultDto<SpaceDto>> GetListAsync([FromQuery] GetSpacesInput input) =>
        _spacesAppService.GetListAsync(input);

    [HttpPost]
    public virtual Task<SpaceDto> CreateAsync([FromBody] CreateSpaceDto input) => _spacesAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual Task<SpaceDto> UpdateAsync(Guid id, [FromBody] UpdateSpaceDto input) =>
        _spacesAppService.UpdateAsync(id, input);

    [HttpPost("{id}/constraints/impact")]
    public virtual Task<ReservationImpactDto> GetConstraintsImpactAsync(Guid id, [FromBody] UpdateSpaceConstraintsDto input) =>
        _spacesAppService.GetConstraintsImpactAsync(id, input);

    [HttpPost("{id}/impact")]
    public virtual Task<ReservationImpactDto> GetUpdateImpactAsync(Guid id, [FromBody] UpdateSpaceDto input) =>
        _spacesAppService.GetUpdateImpactAsync(id, input);

    [HttpGet("{id}/delete-impact")]
    public virtual Task<ReservationImpactDto> GetDeleteImpactAsync(Guid id) => _spacesAppService.GetDeleteImpactAsync(id);

    [HttpPut("{id}/constraints")]
    public virtual Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, [FromBody] UpdateSpaceConstraintsDto input) =>
        _spacesAppService.UpdateConstraintsAsync(id, input);

    [HttpGet("{id}/resolved-constraints")]
    public virtual Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id) =>
        _spacesAppService.GetResolvedConstraintsAsync(id);

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id) => _spacesAppService.DeleteAsync(id);

    [HttpPost("{id}/restore")]
    public virtual Task RestoreAsync(Guid id) => _spacesAppService.RestoreAsync(id);
}
