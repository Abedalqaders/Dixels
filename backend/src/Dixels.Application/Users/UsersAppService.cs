using System;
using System.Threading.Tasks;
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

    public UsersAppService(IIdentityUserRepository identityUserRepository, IRepository<Building, Guid> buildingRepository, IDataFilter dataFilter)
    {
        _dataFilter = dataFilter;
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

    private BuildingDto MapBuildingToDto(Building building)
    {
        var dto = ObjectMapper.Map<Building, BuildingDto>(building);
        dto.Days = ConstraintDtoConversions.ToDayArray(building.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDto(building.Hours);
        dto.IsDeleted = building.IsDeleted;
        return dto;
    }
}
