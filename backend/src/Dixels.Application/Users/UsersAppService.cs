using System;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Users;

[Authorize]
public class UsersAppService : DixelsAppService, IUsersAppService
{
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;

    public UsersAppService(IIdentityUserRepository identityUserRepository, IRepository<Building, Guid> buildingRepository)
    {
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

        // Null when the assigned building was soft-deleted after the assignment — treat
        // this the same as "not assigned" rather than surfacing a broken reference; the
        // admin needs to reassign, not the user.
        var building = await _buildingRepository.FindAsync(buildingId.Value);
        return building is null ? null : MapBuildingToDto(building);
    }

    private BuildingDto MapBuildingToDto(Building building)
    {
        var dto = ObjectMapper.Map<Building, BuildingDto>(building);
        dto.Days = ConstraintDtoConversions.ToDayArray(building.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDto(building.Hours);
        return dto;
    }
}
