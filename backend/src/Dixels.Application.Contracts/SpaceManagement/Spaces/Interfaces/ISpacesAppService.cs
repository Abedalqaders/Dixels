using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Reservations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.SpaceManagement;

public interface ISpacesAppService : IApplicationService
{
    Task<SpaceDto> GetAsync(Guid id);

    /// <summary>Paged, searchable (by name), optionally filtered by space type and/or
    /// including soft-deleted rows — backs the admin Spaces list page (scoped to one
    /// Floor).</summary>
    Task<PagedResultDto<SpaceDto>> GetListAsync(GetSpacesInput input);

    Task<SpaceDto> CreateAsync(CreateSpaceDto input);

    Task<SpaceDto> UpdateAsync(Guid id, UpdateSpaceDto input);

    Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateSpaceConstraintsDto input);

    /// <summary>The upcoming bookings these proposed rules would no longer allow — nothing is saved.</summary>
    Task<ReservationImpactDto> GetConstraintsImpactAsync(Guid id, UpdateSpaceConstraintsDto input, int skip = 0);

    /// <summary>The upcoming bookings a details change (a lower capacity) would no longer allow — nothing is saved.</summary>
    Task<ReservationImpactDto> GetUpdateImpactAsync(Guid id, UpdateSpaceDto input, int skip = 0);

    /// <summary>The upcoming bookings a delete would cancel.</summary>
    Task<ReservationImpactDto> GetDeleteImpactAsync(Guid id, int skip = 0);

    /// <summary>Resolved values (space→floor→building) plus the Building+Floor ancestor
    /// trail, in one round trip.</summary>
    Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id);

    Task DeleteAsync(Guid id);

    Task RestoreAsync(Guid id);
}
