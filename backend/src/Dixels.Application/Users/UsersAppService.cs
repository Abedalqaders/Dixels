using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Localization;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Users;

[Authorize]
public class UsersAppService : DixelsAppService, IUsersAppService
{
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IIdentityRoleRepository _identityRoleRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IdentityUserManager _userManager;
    private readonly BookingImpactService _bookingImpact;
    private readonly LocalizedNameReader _nameReader;

    public UsersAppService(
        IIdentityUserRepository identityUserRepository,
        IIdentityRoleRepository identityRoleRepository,
        IRepository<Building, Guid> buildingRepository,
        IDataFilter dataFilter,
        IdentityUserManager userManager,
        BookingImpactService bookingImpact,
        LocalizedNameReader nameReader)
    {
        _dataFilter = dataFilter;
        _userManager = userManager;
        _bookingImpact = bookingImpact;
        _identityUserRepository = identityUserRepository;
        _identityRoleRepository = identityRoleRepository;
        _buildingRepository = buildingRepository;
        _nameReader = nameReader;
    }

    public async Task<BuildingDto?> GetMyBuildingAsync()
    {
        var user = await _identityUserRepository.FindAsync(CurrentUser.GetId(), includeDetails: false);
        var buildingId = user?.GetBuildingId();
        if (buildingId is null)
        {
            return null;
        }

        // A building deleted since the assignment comes back with IsDeleted set, so the
        // employee is told it was removed rather than "you're not assigned".
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var building = await _buildingRepository.FindAsync(buildingId.Value);
            return building is null ? null : await MapBuildingToDtoAsync(building);
        }
    }

    [Authorize(IdentityPermissions.Users.Update)]
    public async Task<BookingImpactDto> GetReassignImpactAsync(Guid userId)
    {
        var user = await _userManager.GetByIdAsync(userId);
        if (user.GetBuildingId() is not { } current)
        {
            return new BookingImpactDto();
        }

        var (building, upcoming) = await _bookingImpact.UpcomingForUserAsync(userId, current);
        return building is null
            ? new BookingImpactDto()
            : await _bookingImpact.DescribeAsync(building, upcoming, _bookingImpact.Text("Dixels:Bookings:CancelReason:MovedBuilding"));
    }

    [Authorize(IdentityPermissions.Users.Update)]
    public async Task AssignBuildingAsync(Guid userId, AssignUserBuildingDto input)
    {
        var user = await _userManager.GetByIdAsync(userId);
        if (input.BuildingId is { } target)
        {
            await _buildingRepository.GetAsync(target); // 404 for a missing or deleted building
        }

        await _bookingImpact.CancelOnMoveAsync(userId, user.GetBuildingId(), input.BuildingId, CurrentUser.GetId());

        user.SetBuildingId(input.BuildingId);
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new UserFriendlyException(string.Join(" ", result.Errors.Select(e => e.Description)));
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

    private async Task<BuildingDto> MapBuildingToDtoAsync(Building building)
    {
        var dto = ObjectMapper.Map<Building, BuildingDto>(building);
        dto.Name = await _nameReader.ShownAsync(building);
        dto.Names = building.Translations.ToNameDtos();
        dto.Days = ConstraintDtoConversions.ToDayArray(building.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDto(building.Hours);
        dto.IsDeleted = building.IsDeleted;
        return dto;
    }
}
