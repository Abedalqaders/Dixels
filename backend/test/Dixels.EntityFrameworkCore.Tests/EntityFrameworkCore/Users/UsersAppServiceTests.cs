using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Identity;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace Dixels.EntityFrameworkCore.Users;

/// <summary>
/// The user's building through ABP's own user service (replaced by
/// <see cref="DixelsIdentityUserAppService"/>) and our my-building endpoint.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class UsersAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private const string BuildingIdKey = DixelsUserConsts.BuildingIdPropertyName;

    private readonly IIdentityUserAppService _identityUserAppService;
    private readonly IUsersAppService _usersAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly IdentityRoleManager _roleManager;
    private readonly IPermissionManager _permissionManager;

    public UsersAppServiceTests()
    {
        _roleManager = GetRequiredService<IdentityRoleManager>();
        _permissionManager = GetRequiredService<IPermissionManager>();
        _identityUserAppService = GetRequiredService<IIdentityUserAppService>();
        _usersAppService = GetRequiredService<IUsersAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private Task<Building> CreateBuildingAsync() => WithUnitOfWorkAsync(() => _buildingRepository.InsertAsync(new Building(
        Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
        new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
        maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0)));

    // Unique per test: the fixture's database is shared by every test in the run.
    private Task<IdentityUser> CreateUserAsync(Guid? buildingId = null, string? role = null) => WithUnitOfWorkAsync(async () =>
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var user = new IdentityUser(Guid.NewGuid(), "u" + tag, $"{tag}@test.io") { Name = "Test", Surname = tag };
        user.SetBuildingId(buildingId);
        (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
        if (role is not null)
        {
            (await _userManager.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        }

        return user;
    });

    private Task<Guid?> StoredBuildingIdAsync(Guid userId) =>
        WithUnitOfWorkAsync(async () => (await _userManager.GetByIdAsync(userId)).GetBuildingId());

    // What the Users page sends: the user as ABP returned it, with only the building changed —
    // and the id as a string, the way it arrives from JSON.
    private async Task<IdentityUserDto> SetBuildingAsync(Guid userId, string? buildingId)
    {
        var current = await _identityUserAppService.GetAsync(userId);
        var input = new IdentityUserUpdateDto
        {
            UserName = current.UserName,
            Email = current.Email,
            Name = current.Name,
            Surname = current.Surname,
            PhoneNumber = current.PhoneNumber,
            IsActive = current.IsActive,
            LockoutEnabled = current.LockoutEnabled,
            ConcurrencyStamp = current.ConcurrencyStamp,
        };
        input.ExtraProperties[BuildingIdKey] = buildingId;
        return await _identityUserAppService.UpdateAsync(userId, input);
    }

    private IDisposable ActAs(Guid userId)
    {
        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
        })));
    }

    [Fact]
    public async Task Get_returns_the_building_in_extra_properties()
    {
        var building = await CreateBuildingAsync();
        var user = await CreateUserAsync(building.Id);

        var dto = await _identityUserAppService.GetAsync(user.Id);

        Guid.Parse(dto.ExtraProperties[BuildingIdKey]!.ToString()!).ShouldBe(building.Id);
    }

    [Fact]
    public async Task List_filters_by_building()
    {
        var building = await CreateBuildingAsync();
        var assigned = await CreateUserAsync(building.Id);
        await CreateUserAsync();

        var input = new GetIdentityUsersInput();
        input.ExtraProperties[BuildingIdKey] = building.Id.ToString();
        var result = await _identityUserAppService.GetListAsync(input);

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe(assigned.Id);
    }

    [Fact]
    public async Task List_filters_by_role()
    {
        // The surname tag keeps the count to this test's users on the shared database.
        var employee = await CreateUserAsync(role: RoleDataSeedContributor.EmployeeRoleName);
        var noRole = await CreateUserAsync();

        var input = new GetIdentityUsersInput { Filter = employee.Surname };
        input.ExtraProperties[DixelsUserConsts.RoleFilterKey] = RoleDataSeedContributor.EmployeeRoleName;
        var result = await _identityUserAppService.GetListAsync(input);

        result.Items.ShouldHaveSingleItem().Id.ShouldBe(employee.Id);

        input.Filter = noRole.Surname;
        (await _identityUserAppService.GetListAsync(input)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_filters_by_role_and_building_together()
    {
        var building = await CreateBuildingAsync();
        var employeeHere = await CreateUserAsync(building.Id, RoleDataSeedContributor.EmployeeRoleName);
        await CreateUserAsync(building.Id);
        await CreateUserAsync(role: RoleDataSeedContributor.EmployeeRoleName);

        var input = new GetIdentityUsersInput();
        input.ExtraProperties[BuildingIdKey] = building.Id.ToString();
        input.ExtraProperties[DixelsUserConsts.RoleFilterKey] = RoleDataSeedContributor.EmployeeRoleName;
        var result = await _identityUserAppService.GetListAsync(input);

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe(employeeHere.Id);
    }

    [Fact]
    public async Task List_for_an_unknown_role_is_empty()
    {
        var input = new GetIdentityUsersInput();
        input.ExtraProperties[DixelsUserConsts.RoleFilterKey] = "no-such-role";

        (await _identityUserAppService.GetListAsync(input)).TotalCount.ShouldBe(0);
    }

    // ---- Filtering by a permission (the Users page: everyone who can book) ----

    private Task<string> CreateRoleAsync(params string[] grants) => WithUnitOfWorkAsync(async () =>
    {
        var name = "role" + Guid.NewGuid().ToString("N")[..8];
        (await _roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), name))).Succeeded.ShouldBeTrue();
        foreach (var grant in grants)
        {
            await _permissionManager.SetAsync(grant, RolePermissionValueProvider.ProviderName, name, true);
        }

        return name;
    });

    private Task<PagedResultDto<IdentityUserDto>> ListHoldingAsync(string permission, string filter)
    {
        var input = new GetIdentityUsersInput { Filter = filter };
        input.ExtraProperties[DixelsUserConsts.PermissionFilterKey] = permission;
        return _identityUserAppService.GetListAsync(input);
    }

    [Fact]
    public async Task List_filters_by_a_permission_granted_through_any_role()
    {
        var bookerRole = await CreateRoleAsync(DixelsPermissions.Bookings.Default, DixelsPermissions.Bookings.Create);
        var viewerRole = await CreateRoleAsync(DixelsPermissions.Bookings.Default);
        var booker = await CreateUserAsync(role: bookerRole);
        var employee = await CreateUserAsync(role: RoleDataSeedContributor.EmployeeRoleName);
        var viewer = await CreateUserAsync(role: viewerRole);
        var noRole = await CreateUserAsync();

        (await ListHoldingAsync(DixelsPermissions.Bookings.Create, booker.Surname!)).Items.ShouldHaveSingleItem().Id.ShouldBe(booker.Id);
        (await ListHoldingAsync(DixelsPermissions.Bookings.Create, employee.Surname!)).Items.ShouldHaveSingleItem().Id.ShouldBe(employee.Id);
        (await ListHoldingAsync(DixelsPermissions.Bookings.Create, viewer.Surname!)).TotalCount.ShouldBe(0);
        (await ListHoldingAsync(DixelsPermissions.Bookings.Create, noRole.Surname!)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_by_permission_includes_admins_while_their_role_can_book()
    {
        // ABP's seed grants the admin role every permission, booking included.
        var admin = await CreateUserAsync(role: "admin");

        (await ListHoldingAsync(DixelsPermissions.Bookings.Create, admin.Surname!)).Items.ShouldHaveSingleItem().Id.ShouldBe(admin.Id);
    }

    [Fact]
    public async Task List_by_permission_includes_a_direct_grant_and_drops_it_once_revoked()
    {
        var user = await CreateUserAsync();
        await WithUnitOfWorkAsync(() =>
            _permissionManager.SetAsync(DixelsPermissions.Bookings.Create, UserPermissionValueProvider.ProviderName, user.Id.ToString(), true));

        (await ListHoldingAsync(DixelsPermissions.Bookings.Create, user.Surname!)).Items.ShouldHaveSingleItem().Id.ShouldBe(user.Id);

        await WithUnitOfWorkAsync(() =>
            _permissionManager.SetAsync(DixelsPermissions.Bookings.Create, UserPermissionValueProvider.ProviderName, user.Id.ToString(), false));

        (await ListHoldingAsync(DixelsPermissions.Bookings.Create, user.Surname!)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_by_permission_and_building_together()
    {
        var building = await CreateBuildingAsync();
        var here = await CreateUserAsync(building.Id, RoleDataSeedContributor.EmployeeRoleName);
        var elsewhere = await CreateUserAsync(role: RoleDataSeedContributor.EmployeeRoleName);

        var input = new GetIdentityUsersInput();
        input.ExtraProperties[DixelsUserConsts.PermissionFilterKey] = DixelsPermissions.Bookings.Create;
        input.ExtraProperties[BuildingIdKey] = building.Id.ToString();
        var result = await _identityUserAppService.GetListAsync(input);

        result.Items.ShouldHaveSingleItem().Id.ShouldBe(here.Id);
        result.Items.ShouldNotContain(u => u.Id == elsewhere.Id);
    }

    [Fact]
    public async Task List_without_a_building_filter_is_abps_own()
    {
        var result = await _identityUserAppService.GetListAsync(new GetIdentityUsersInput { Filter = "admin" });

        result.Items.ShouldContain(u => u.UserName == "admin");
    }

    [Fact]
    public async Task Update_assigns_then_clears_the_building()
    {
        var building = await CreateBuildingAsync();
        var user = await CreateUserAsync();

        await SetBuildingAsync(user.Id, building.Id.ToString());
        (await StoredBuildingIdAsync(user.Id)).ShouldBe(building.Id);

        await SetBuildingAsync(user.Id, null);
        (await StoredBuildingIdAsync(user.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Update_keeps_the_users_roles()
    {
        var building = await CreateBuildingAsync();
        var user = await CreateUserAsync();
        await WithUnitOfWorkAsync(async () =>
            (await _userManager.AddToRoleAsync(await _userManager.GetByIdAsync(user.Id), "employee")).Succeeded.ShouldBeTrue());

        await SetBuildingAsync(user.Id, building.Id.ToString());

        (await _identityUserAppService.GetRolesAsync(user.Id)).Items.ShouldContain(r => r.Name == "employee");
    }

    [Fact]
    public async Task Update_to_a_missing_building_is_not_found()
    {
        var user = await CreateUserAsync();

        await Should.ThrowAsync<EntityNotFoundException>(() => SetBuildingAsync(user.Id, Guid.NewGuid().ToString()));
    }

    // ---- Building names and roles for the Users page (batch, not one call per row) ----

    private Task<List<UserPageDetailsDto>> PageDetailsAsync(params Guid[] userIds) =>
        _usersAppService.GetPageDetailsAsync(new GetUserPageDetailsInput { UserIds = userIds.ToList() });

    [Fact]
    public async Task Page_details_return_each_ones_role_names_in_one_call()
    {
        var employee = await CreateUserAsync(role: RoleDataSeedContributor.EmployeeRoleName);
        var noRole = await CreateUserAsync();

        var result = await PageDetailsAsync(employee.Id, noRole.Id);

        result.Single(r => r.UserId == employee.Id).Roles.ShouldContain(RoleDataSeedContributor.EmployeeRoleName);
        result.Single(r => r.UserId == noRole.Id).Roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task Page_details_name_each_users_building_and_leave_the_unassigned_empty()
    {
        var building = await CreateBuildingAsync();
        var assigned = await CreateUserAsync(building.Id);
        var sameBuilding = await CreateUserAsync(building.Id);
        var unassigned = await CreateUserAsync();

        var result = await PageDetailsAsync(assigned.Id, sameBuilding.Id, unassigned.Id);

        var name = building.Translations.Single().Name;
        result.Single(r => r.UserId == assigned.Id).ShouldSatisfyAllConditions(
            r => r.BuildingName.ShouldBe(name),
            r => r.BuildingRemoved.ShouldBeFalse());
        result.Single(r => r.UserId == sameBuilding.Id).BuildingName.ShouldBe(name);
        result.Single(r => r.UserId == unassigned.Id).ShouldSatisfyAllConditions(
            r => r.BuildingName.ShouldBeNull(),
            r => r.BuildingRemoved.ShouldBeFalse());
    }

    [Fact]
    public async Task Page_details_flag_a_deleted_building_and_keep_its_last_name()
    {
        var building = await CreateBuildingAsync();
        var user = await CreateUserAsync(building.Id);
        await WithUnitOfWorkAsync(() => _buildingRepository.DeleteAsync(building.Id));

        var row = (await PageDetailsAsync(user.Id)).Single();

        row.BuildingRemoved.ShouldBeTrue();
        row.BuildingName.ShouldBe(building.Translations.Single().Name);
    }

    [Fact]
    public async Task Page_details_flag_a_building_that_cannot_be_found()
    {
        var user = await CreateUserAsync(Guid.NewGuid());

        var row = (await PageDetailsAsync(user.Id)).Single();

        row.BuildingRemoved.ShouldBeTrue();
        row.BuildingName.ShouldBeNull();
    }

    [Fact]
    public async Task Page_details_skip_an_id_that_no_longer_exists()
    {
        (await PageDetailsAsync(Guid.NewGuid())).ShouldBeEmpty();
    }

    [Fact]
    public async Task Page_details_refuse_more_than_a_page_of_users()
    {
        var ids = Enumerable.Range(0, GetUserPageDetailsInput.MaxUserIds + 1).Select(_ => Guid.NewGuid()).ToArray();

        await Should.ThrowAsync<AbpValidationException>(() => PageDetailsAsync(ids));
    }

    [Fact]
    public async Task Role_names_lists_every_role_including_the_seeded_ones()
    {
        var customRole = await CreateRoleAsync();

        var names = await _usersAppService.GetRoleNamesAsync();

        names.ShouldContain(RoleDataSeedContributor.EmployeeRoleName);
        names.ShouldContain("admin");
        names.ShouldContain(customRole);
    }
}
