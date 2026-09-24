using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.SpaceManagement;

[Authorize(DixelsPermissions.Overrides.Default)]
public class AvailabilityOverridesAppService : DixelsAppService, IAvailabilityOverridesAppService
{
    private readonly IRepository<AvailabilityOverride, Guid> _overrideRepository;

    public AvailabilityOverridesAppService(IRepository<AvailabilityOverride, Guid> overrideRepository)
    {
        _overrideRepository = overrideRepository;
    }

    public async Task<ListResultDto<AvailabilityOverrideDto>> GetListAsync(OverrideScope scope, Guid scopeId)
    {
        var overrides = await _overrideRepository.GetListAsync(o => o.Scope == scope && o.ScopeId == scopeId);

        return new ListResultDto<AvailabilityOverrideDto>(
            overrides.OrderBy(o => o.StartsAt).Select(o => ObjectMapper.Map<AvailabilityOverride, AvailabilityOverrideDto>(o)).ToList());
    }

    [Authorize(DixelsPermissions.Overrides.Create)]
    public async Task<AvailabilityOverrideDto> CreateAsync(CreateAvailabilityOverrideDto input)
    {
        var availabilityOverride = new AvailabilityOverride(
            GuidGenerator.Create(),
            input.Scope,
            input.ScopeId,
            input.StartsAt,
            input.EndsAt,
            input.Effect,
            input.ReasonCategory,
            input.ReasonDetail);

        await _overrideRepository.InsertAsync(availabilityOverride);

        return ObjectMapper.Map<AvailabilityOverride, AvailabilityOverrideDto>(availabilityOverride);
    }

    [Authorize(DixelsPermissions.Overrides.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        // A real hard delete — AvailabilityOverride is AuditedAggregateRoot, not
        // FullAuditedAggregateRoot, so there's no soft-delete interceptor to convert this;
        // matches the entity's own "delete+recreate, not in-place edit" design.
        await _overrideRepository.DeleteAsync(id);
    }
}
