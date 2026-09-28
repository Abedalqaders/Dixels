using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
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
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IdentityUserManager _userManager;
    private readonly BookingImpactService _bookingImpact;

    public UsersAppService(
        IIdentityUserRepository identityUserRepository,
        IRepository<Building, Guid> buildingRepository,
        IDataFilter dataFilter,
        IdentityUserManager userManager,
        BookingImpactService bookingImpact)
    {
        _dataFilter = dataFilter;
        _userManager = userManager;
        _bookingImpact = bookingImpact;
        _identityUserRepository = identityUserRepository;
        _buildingRepository = buildingRepository;
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
            return building is null ? null : MapBuildingToDto(building);
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

        var current = user.GetBuildingId();
        if (current is not null && current != input.BuildingId && input.CancelUpcomingBookings)
        {
            var (_, upcoming) = await _bookingImpact.UpcomingForUserAsync(userId, current);
            await _bookingImpact.CancelAllAsync(upcoming, CurrentUser.GetId(), _bookingImpact.Text("Dixels:Bookings:CancelReason:MovedBuilding"));
        }

        user.SetBuildingId(input.BuildingId);
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new UserFriendlyException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    private BuildingDto MapBuildingToDto(Building building)
    {
        var dto = ObjectMapper.Map<Building, BuildingDto>(building);
        dto.Days = ConstraintDtoConversions.ToDayArray(building.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDto(building.Hours);
        dto.IsDeleted = building.IsDeleted;
        return dto;
    }
}
