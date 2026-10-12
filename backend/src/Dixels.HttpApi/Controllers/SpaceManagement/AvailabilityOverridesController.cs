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
    public virtual async Task<PagedResultDto<AvailabilityOverrideDto>> GetListAsync([FromQuery] GetAvailabilityOverridesInput input) =>
        await _overridesAppService.GetListAsync(input);

    [HttpGet("active")]
    public virtual async Task<ListResultDto<AvailabilityOverrideDto>> GetActiveAsync([FromQuery] OverrideScope scope, [FromQuery] Guid scopeId) =>
        await _overridesAppService.GetActiveAsync(scope, scopeId);

    [HttpPost]
    public virtual async Task<AvailabilityOverrideDto> CreateAsync([FromBody] CreateAvailabilityOverrideDto input) =>
        await _overridesAppService.CreateAsync(input);

    [HttpPost("impact")]
    public virtual async Task<ReservationImpactDto> GetCreateImpactAsync([FromBody] CreateAvailabilityOverrideDto input, [FromQuery] int skip = 0) =>
        await _overridesAppService.GetCreateImpactAsync(input, skip);

    [HttpDelete("{id}")]
    public virtual async Task DeleteAsync(Guid id) => await _overridesAppService.DeleteAsync(id);
}
