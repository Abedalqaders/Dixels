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
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Users;

/// <summary>
/// ABP's own user service (<c>/api/identity/users</c>), extended for the user's building —
/// the <see cref="DixelsUserConsts.BuildingIdPropertyName"/> extra property:
/// <list type="bullet">
/// <item>the list can be filtered by it: <c>?ExtraProperties[BuildingId]={id}</c>, and by
/// role: <c>?ExtraProperties[Role]=employee</c>, and by a permission its users hold:
/// <c>?ExtraProperties[Permission]=Dixels.Bookings.Create</c> (the Users page lists everyone who
/// can book, since that's who needs a building);</item>
/// <item>create/update check that the building it names exists.</item>
/// </list>
/// Deactivating, deleting or moving someone is announced (UserEvents) for whatever they
/// hold — their bookings — to be released by its own module.
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
    private readonly ILocalEventBus _localEventBus;

    public DixelsIdentityUserAppService(
        IdentityUserManager userManager,
        IIdentityUserRepository userRepository,
        IIdentityRoleRepository roleRepository,
        IOptions<Microsoft.AspNetCore.Identity.IdentityOptions> identityOptions,
        IPermissionChecker permissionChecker,
        IUserDirectoryRepository userDirectoryRepository,
        IRepository<Building, Guid> buildingRepository,
        ILocalEventBus localEventBus)
        : base(userManager, userRepository, roleRepository, identityOptions, permissionChecker)
    {
        _userDirectoryRepository = userDirectoryRepository;
        _buildingRepository = buildingRepository;
        _localEventBus = localEventBus;
    }

    [Authorize(IdentityPermissions.Users.Default)]
    public override async Task<PagedResultDto<IdentityUserDto>> GetListAsync(GetIdentityUsersInput input)
    {
        var (_, buildingId) = ReadBuildingId(input.ExtraProperties);
        var roleName = ReadString(input.ExtraProperties, DixelsUserConsts.RoleFilterKey);
        var permissionName = ReadString(input.ExtraProperties, DixelsUserConsts.PermissionFilterKey);
        if (buildingId is null && roleName is null && permissionName is null)
        {
            return await base.GetListAsync(input);
        }

        Guid? roleId = null;
        if (roleName is not null)
        {
            var role = await RoleRepository.FindByNormalizedNameAsync(UserManager.NormalizeName(roleName));
            if (role is null)
            {
                // A role that doesn't exist has no members — an empty page, not an error.
                return new PagedResultDto<IdentityUserDto>(0, new List<IdentityUserDto>());
            }

            roleId = role.Id;
        }

        // ABP's IIdentityUserRepository can filter by neither an extra-property column nor a
        // role or permission, so a filtered list goes through our own query (same text filter semantics).
        var count = await _userDirectoryRepository.GetCountAsync(input.Filter, buildingId, roleId, permissionName);
        var users = await _userDirectoryRepository.GetListAsync(
            input.Filter, buildingId, roleId, permissionName, input.SkipCount, input.MaxResultCount);

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

        var before = await UserManager.GetByIdAsync(id);
        var wasActive = before.IsActive;
        var oldBuildingId = before.GetBuildingId();
        var (buildingSent, newBuildingId) = ReadBuildingId(input.ExtraProperties);

        var result = await base.UpdateAsync(id, input);
        if (wasActive && !input.IsActive)
        {
            await _localEventBus.PublishAsync(new UserDeactivatedEvent(id, CurrentUser.GetId()));
        }
        else if (buildingSent && oldBuildingId != newBuildingId)
        {
            // Moved through the account form rather than the Users page: same announcement.
            await _localEventBus.PublishAsync(new UserMovedBuildingEvent(id, oldBuildingId, newBuildingId, CurrentUser.GetId()));
        }

        return result;
    }

    [Authorize(IdentityPermissions.Users.Delete)]
    public override async Task DeleteAsync(Guid id)
    {
        await base.DeleteAsync(id);
        await _localEventBus.PublishAsync(new UserDeletedEvent(id, CurrentUser.GetId()));
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

    private static string? ReadString(ExtraPropertyDictionary extraProperties, string key)
    {
        if (!extraProperties.TryGetValue(key, out var raw))
        {
            return null;
        }

        var text = raw switch
        {
            null => null,
            JsonElement { ValueKind: JsonValueKind.Null } => null,
            JsonElement e => e.ToString(),
            _ => raw.ToString(),
        };

        return text.IsNullOrWhiteSpace() ? null : text!.Trim();
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
