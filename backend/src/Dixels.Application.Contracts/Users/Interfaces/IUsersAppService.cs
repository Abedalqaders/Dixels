using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Volo.Abp.Application.Services;

namespace Dixels.Users;

/// <summary>
/// The one user endpoint ABP's Identity module doesn't have. Listing users and setting
/// their building go through ABP's own <c>/api/identity/users</c> (see
/// <c>DixelsIdentityUserAppService</c>), with the building in <c>extraProperties.BuildingId</c>.
/// </summary>
public interface IUsersAppService : IApplicationService
{
    /// <summary>The current user's own assigned building, or null if they haven't been
    /// assigned one yet (or it was deleted). Available to any authenticated user.</summary>
    Task<BuildingDto?> GetMyBuildingAsync();

    /// <summary>An employee's upcoming bookings in the building they're assigned to now — what moving them would leave behind.</summary>
    Task<BookingImpactDto> GetReassignImpactAsync(Guid userId);

    /// <summary>
    /// Sets (or clears) the employee's building, optionally cancelling their upcoming bookings
    /// in the one they're leaving — both in one go.
    /// </summary>
    Task AssignBuildingAsync(Guid userId, AssignUserBuildingDto input);
}
