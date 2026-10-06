using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Users;

[Authorize]
public class UsersAppService : DixelsAppService, IUsersAppService
{
    private readonly IIdentityRoleRepository _identityRoleRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IdentityUserManager _userManager;
    private readonly LocalizedNameReader _nameReader;
    private readonly IDataFilter _dataFilter;
    // Only for the preview (GetReassignImpactAsync): the move itself is announced.
    private readonly ReservationImpactPreview _impactPreview;
    private readonly ILocalEventBus _localEventBus;

    public UsersAppService(
        IIdentityRoleRepository identityRoleRepository,
        IIdentityUserRepository identityUserRepository,
        IRepository<Building, Guid> buildingRepository,
        IdentityUserManager userManager,
        LocalizedNameReader nameReader,
        IDataFilter dataFilter,
        ReservationImpactPreview impactPreview,
        ILocalEventBus localEventBus)
    {
        _userManager = userManager;
        _impactPreview = impactPreview;
        _localEventBus = localEventBus;
        _identityRoleRepository = identityRoleRepository;
        _identityUserRepository = identityUserRepository;
        _buildingRepository = buildingRepository;
        _nameReader = nameReader;
        _dataFilter = dataFilter;
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
    public async Task<List<UserPageDetailsDto>> GetPageDetailsAsync(GetUserPageDetailsInput input)
    {
        var userIds = input.UserIds.Distinct().ToList();
        if (userIds.Count == 0)
        {
            return new List<UserPageDetailsDto>();
        }

        // Three queries for the whole page, however many rows: the users (for their building),
        // the buildings, and every user's roles (ABP's batch query, roles through an
        // organization unit included). An id that no longer exists is skipped.
        var users = await _identityUserRepository.GetListByIdsAsync(userIds);
        var buildingIds = users.Select(u => u.GetBuildingId()).OfType<Guid>().Distinct().ToList();

        List<Building> buildings;
        // Deleted ones too: someone whose building was removed shows its last name, flagged.
        using (_dataFilter.Disable<ISoftDelete>())
        {
            buildings = buildingIds.Count == 0
                ? new List<Building>()
                : await _buildingRepository.GetListAsync(b => buildingIds.Contains(b.Id), includeDetails: true);
        }

        var buildingNames = await _nameReader.ShownAsync<Building, BuildingTranslation>(buildings);
        // Removed: deleted since, or not found at all — either way they need a new one.
        var liveBuildingIds = buildings.Where(b => !b.IsDeleted).Select(b => b.Id).ToHashSet();
        var rolesByUserId = (await _identityUserRepository.GetRoleNamesAsync(users.Select(u => u.Id)))
            .ToDictionary(r => r.Id, r => r.RoleNames);

        return users.Select(user =>
        {
            var buildingId = user.GetBuildingId();
            return new UserPageDetailsDto
            {
                UserId = user.Id,
                BuildingName = buildingId is { } id ? buildingNames.GetValueOrDefault(id) : null,
                BuildingRemoved = buildingId is { } assigned && !liveBuildingIds.Contains(assigned),
                Roles = rolesByUserId.TryGetValue(user.Id, out var roles) ? roles.OrderBy(name => name).ToList() : new List<string>(),
            };
        }).ToList();
    }

    [Authorize(IdentityPermissions.Users.Default)]
    public async Task<List<string>> GetRoleNamesAsync()
    {
        var roles = await _identityRoleRepository.GetListAsync();
        return roles.Select(r => r.Name).OrderBy(name => name).ToList();
    }
}
