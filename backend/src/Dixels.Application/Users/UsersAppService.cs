using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Users;

[Authorize]
public class UsersAppService : DixelsAppService, IUsersAppService
{
    private readonly IIdentityRoleRepository _identityRoleRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IdentityUserManager _userManager;
    // Only for the preview (GetReassignImpactAsync): the move itself is announced.
    private readonly ReservationImpactPreview _impactPreview;
    private readonly ILocalEventBus _localEventBus;

    public UsersAppService(
        IIdentityRoleRepository identityRoleRepository,
        IRepository<Building, Guid> buildingRepository,
        IdentityUserManager userManager,
        ReservationImpactPreview impactPreview,
        ILocalEventBus localEventBus)
    {
        _userManager = userManager;
        _impactPreview = impactPreview;
        _localEventBus = localEventBus;
        _identityRoleRepository = identityRoleRepository;
        _buildingRepository = buildingRepository;
    }

    [Authorize(IdentityPermissions.Users.Update)]
    public async Task<ReservationImpactDto> GetReassignImpactAsync(Guid userId)
    {
        var user = await _userManager.GetByIdAsync(userId);
        if (user.GetBuildingId() is not { } current)
        {
            return new ReservationImpactDto();
        }

        // A deleted building reads as not found: nothing is held there that a move could release.
        var building = await _buildingRepository.FindAsync(current);
        return building is null
            ? new ReservationImpactDto()
            : await _impactPreview.PersonLeavingAsync(userId, building, L["Dixels:Bookings:CancelReason:MovedBuilding"]);
    }

    [Authorize(IdentityPermissions.Users.Update)]
    public async Task AssignBuildingAsync(Guid userId, AssignUserBuildingDto input)
    {
        var user = await _userManager.GetByIdAsync(userId);
        if (input.BuildingId is { } target)
        {
            await _buildingRepository.GetAsync(target); // 404 for a missing or deleted building
        }

        var fromBuildingId = user.GetBuildingId();
        user.SetBuildingId(input.BuildingId);
        // ABP's own exception for Identity errors: each one is reported and translated, not
        // glued together in English.
        (await _userManager.UpdateAsync(user)).CheckErrors();

        if (fromBuildingId != input.BuildingId)
        {
            await _localEventBus.PublishAsync(new UserMovedBuildingEvent(userId, fromBuildingId, input.BuildingId, CurrentUser.GetId()));
        }
    }

    [Authorize(IdentityPermissions.Users.Default)]
    public async Task<List<UserRolesDto>> GetRolesForUsersAsync(List<Guid> userIds)
    {
        var result = new List<UserRolesDto>();
        foreach (var id in userIds.Distinct())
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                continue;
            }

            result.Add(new UserRolesDto { UserId = id, Roles = (await _userManager.GetRolesAsync(user)).ToList() });
        }

        return result;
    }

    [Authorize(IdentityPermissions.Users.Default)]
    public async Task<List<string>> GetRoleNamesAsync()
    {
        var roles = await _identityRoleRepository.GetListAsync();
        return roles.Select(r => r.Name).OrderBy(name => name).ToList();
    }
}
