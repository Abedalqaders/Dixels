using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceManagement;

/// <summary>Managed as delete+recreate, not in-place edit — matching the entity's own
/// design, there is deliberately no UpdateAsync here.</summary>
public interface IAvailabilityOverridesAppService : IApplicationService
{
    Task<ListResultDto<AvailabilityOverrideDto>> GetListAsync(OverrideScope scope, Guid scopeId);

    Task<AvailabilityOverrideDto> CreateAsync(CreateAvailabilityOverrideDto input);

    Task DeleteAsync(Guid id);
}
