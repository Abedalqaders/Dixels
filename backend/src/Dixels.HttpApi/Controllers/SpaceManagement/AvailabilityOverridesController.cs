using System;
using System.Threading.Tasks;
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
    public virtual Task<ListResultDto<AvailabilityOverrideDto>> GetListAsync([FromQuery] OverrideScope scope, [FromQuery] Guid scopeId) =>
        _overridesAppService.GetListAsync(scope, scopeId);

    [HttpPost]
    public virtual Task<AvailabilityOverrideDto> CreateAsync([FromBody] CreateAvailabilityOverrideDto input) =>
        _overridesAppService.CreateAsync(input);

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id) => _overridesAppService.DeleteAsync(id);
}
