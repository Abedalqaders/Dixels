using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Dixels.Users;

/// <summary>
/// ABP's own user service (<c>/api/identity/users</c>), extended for the user's building —
/// the <see cref="DixelsUserConsts.BuildingIdPropertyName"/> extra property:
/// <list type="bullet">
/// <item>the list can be filtered by it: <c>?ExtraProperties[BuildingId]={id}</c>;</item>
/// <item>create/update check that the building it names exists.</item>
/// </list>
/// Everything else is ABP's behaviour, unchanged. Reading and writing the value itself needs
/// no code here: ABP's extension system already carries it in each DTO's
/// <c>extraProperties</c> (see DixelsModuleExtensionConfigurator).
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IIdentityUserAppService), typeof(IdentityUserAppService), typeof(DixelsIdentityUserAppService))]
public class DixelsIdentityUserAppService : IdentityUserAppService
{
    private readonly IUserDirectoryRepository _userDirectoryRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;

    public DixelsIdentityUserAppService(
        IdentityUserManager userManager,
        IIdentityUserRepository userRepository,
        IIdentityRoleRepository roleRepository,
        IOptions<Microsoft.AspNetCore.Identity.IdentityOptions> identityOptions,
        IPermissionChecker permissionChecker,
        IUserDirectoryRepository userDirectoryRepository,
        IRepository<Building, Guid> buildingRepository)
        : base(userManager, userRepository, roleRepository, identityOptions, permissionChecker)
    {
        _userDirectoryRepository = userDirectoryRepository;
        _buildingRepository = buildingRepository;
    }

    [Authorize(IdentityPermissions.Users.Default)]
    public override async Task<PagedResultDto<IdentityUserDto>> GetListAsync(GetIdentityUsersInput input)
    {
        var (hasBuildingFilter, buildingId) = ReadBuildingId(input.ExtraProperties);
        if (!hasBuildingFilter || buildingId is null)
        {
            return await base.GetListAsync(input);
        }

        // ABP's IIdentityUserRepository can't filter on an extra-property column, so a
        // building-filtered list goes through our own query (same text filter semantics).
        var count = await _userDirectoryRepository.GetCountAsync(input.Filter, buildingId);
        var users = await _userDirectoryRepository.GetListAsync(input.Filter, buildingId, input.SkipCount, input.MaxResultCount);

        return new PagedResultDto<IdentityUserDto>(count, ObjectMapper.Map<List<IdentityUser>, List<IdentityUserDto>>(users));
    }

    [Authorize(IdentityPermissions.Users.Create)]
    public override async Task<IdentityUserDto> CreateAsync(IdentityUserCreateDto input)
    {
        await NormalizeAndCheckBuildingAsync(input.ExtraProperties);
        return await base.CreateAsync(input);
    }

    [Authorize(IdentityPermissions.Users.Update)]
    public override async Task<IdentityUserDto> UpdateAsync(Guid id, IdentityUserUpdateDto input)
    {
        await NormalizeAndCheckBuildingAsync(input.ExtraProperties);
        return await base.UpdateAsync(id, input);
    }

    /// <summary>
    /// JSON bodies and query strings bring the id in as a string (or JsonElement); store it
    /// as a real Guid, and refuse an id that isn't a building. GetAsync throws ABP's own
    /// EntityNotFoundException (404) for a missing or soft-deleted one.
    /// </summary>
    private async Task NormalizeAndCheckBuildingAsync(ExtraPropertyDictionary extraProperties)
    {
        var (present, buildingId) = ReadBuildingId(extraProperties);
        if (!present)
        {
            return;
        }

        if (buildingId is not null)
        {
            await _buildingRepository.GetAsync(buildingId.Value);
        }

        extraProperties[DixelsUserConsts.BuildingIdPropertyName] = buildingId;
    }

    private static (bool Present, Guid? BuildingId) ReadBuildingId(ExtraPropertyDictionary extraProperties)
    {
        if (!extraProperties.TryGetValue(DixelsUserConsts.BuildingIdPropertyName, out var raw))
        {
            return (false, null);
        }

        var text = raw switch
        {
            null => null,
            Guid g => g.ToString(),
            JsonElement { ValueKind: JsonValueKind.Null } => null,
            JsonElement e => e.ToString(),
            _ => raw.ToString(),
        };

        if (text.IsNullOrWhiteSpace())
        {
            return (true, null);
        }

        return Guid.TryParse(text, out var id)
            ? (true, id)
            : throw new BusinessException(DixelsDomainErrorCodes.InvalidBuildingId);
    }
}
