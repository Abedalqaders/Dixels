using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Reservations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceManagement;

/// <summary>Managed as delete+recreate, not in-place edit — matching the entity's own
/// design, there is deliberately no UpdateAsync here.</summary>
public interface IAvailabilityOverridesAppService : IApplicationService
{
    /// <summary>One page of a building's, floor's or space's own closures — for showing them.</summary>
    Task<PagedResultDto<AvailabilityOverrideDto>> GetListAsync(GetAvailabilityOverridesInput input);

    /// <summary>
    /// Every closure and special opening in effect right now, unpaged — what "closed now" is
    /// worked out from, which a page of the list could miss. Only what's on at this moment,
    /// so it stays a row or two.
    /// </summary>
    Task<ListResultDto<AvailabilityOverrideDto>> GetActiveAsync(OverrideScope scope, Guid scopeId);

    Task<AvailabilityOverrideDto> CreateAsync(CreateAvailabilityOverrideDto input);

    /// <summary>The upcoming bookings this closure would fall on — nothing is saved.</summary>
    Task<ReservationImpactDto> GetCreateImpactAsync(CreateAvailabilityOverrideDto input, int skip = 0);

    Task DeleteAsync(Guid id);
}
