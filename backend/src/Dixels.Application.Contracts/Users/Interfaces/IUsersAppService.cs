using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Reservations;
using Volo.Abp.Application.Services;

namespace Dixels.Users;

/// <summary>
/// User endpoints ABP's Identity module doesn't have. Listing users goes through ABP's own
/// <c>/api/identity/users</c> (see <c>DixelsIdentityUserAppService</c>), which also accepts the
/// building in <c>extraProperties.BuildingId</c>; the Users page sets it with
/// <see cref="AssignBuildingAsync"/>, which also cancels what moving leaves behind.
/// A single user's roles are ABP's own <c>/api/identity/users/{id}/roles</c>. The Users page's
/// Roles column and role filter use the two below instead, so it needs neither one call per
/// row nor the separate Role-management permission <c>/api/identity/roles</c> would require.
/// </summary>
public interface IUsersAppService : IApplicationService
{
    /// <summary>An employee's upcoming bookings in the building they're assigned to now — what moving them would leave behind.</summary>
    Task<ReservationImpactDto> GetReassignImpactAsync(Guid userId);

    /// <summary>
    /// Sets (or clears) the employee's building, optionally cancelling their upcoming bookings
    /// in the one they're leaving — both in one go.
    /// </summary>
    Task AssignBuildingAsync(Guid userId, AssignUserBuildingDto input);

    /// <summary>Each of these users' role names, one batch call for a page of the Users list.</summary>
    Task<List<UserRolesDto>> GetRolesForUsersAsync(List<Guid> userIds);

    /// <summary>Every role name in the system, for the Users page's role filter.</summary>
    Task<List<string>> GetRoleNamesAsync();
}
