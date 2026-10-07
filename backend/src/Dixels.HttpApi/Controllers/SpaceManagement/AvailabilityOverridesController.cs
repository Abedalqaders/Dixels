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
[Route("api/app/availability-overrides")]
public class AvailabilityOverridesController : DixelsController, IAvailabilityOverridesAppService
{
    private readonly IAvailabilityOverridesAppService _overridesAppService;

    public AvailabilityOverridesController(IAvailabilityOverridesAppService overridesAppService)
    {
        _overridesAppService = overridesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AvailabilityOverrideDto>> GetListAsync([FromQuery] GetAvailabilityOverridesInput input) =>
        _overridesAppService.GetListAsync(input);

    [HttpGet("active")]
    public virtual Task<ListResultDto<AvailabilityOverrideDto>> GetActiveAsync([FromQuery] OverrideScope scope, [FromQuery] Guid scopeId) =>
        _overridesAppService.GetActiveAsync(scope, scopeId);

    [HttpPost]
    public virtual Task<AvailabilityOverrideDto> CreateAsync([FromBody] CreateAvailabilityOverrideDto input) =>
        _overridesAppService.CreateAsync(input);

    [HttpPost("impact")]
    public virtual Task<ReservationImpactDto> GetCreateImpactAsync([FromBody] CreateAvailabilityOverrideDto input, [FromQuery] int skip = 0) =>
        _overridesAppService.GetCreateImpactAsync(input, skip);

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id) => _overridesAppService.DeleteAsync(id);
}
