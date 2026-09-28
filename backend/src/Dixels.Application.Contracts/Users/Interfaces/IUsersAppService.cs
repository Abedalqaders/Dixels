using System.Threading.Tasks;
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
}
