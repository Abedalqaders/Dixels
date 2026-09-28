using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Identity;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
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

    public UsersAppServiceTests()
    {
        _identityUserAppService = GetRequiredService<IIdentityUserAppService>();
        _usersAppService = GetRequiredService<IUsersAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private Task<Building> CreateBuildingAsync() => WithUnitOfWorkAsync(() => _buildingRepository.InsertAsync(new Building(
        Guid.NewGuid(), "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
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

    [Fact]
    public async Task My_building_returns_the_assigned_one()
    {
        var building = await CreateBuildingAsync();
        var user = await CreateUserAsync(building.Id);
        using var _ = ActAs(user.Id);

        var mine = await _usersAppService.GetMyBuildingAsync();

        mine.ShouldNotBeNull();
        mine.Id.ShouldBe(building.Id);
    }

    [Fact]
    public async Task My_building_is_null_once_the_building_is_soft_deleted()
    {
        var building = await CreateBuildingAsync();
        var user = await CreateUserAsync(building.Id);
        await WithUnitOfWorkAsync(() => _buildingRepository.DeleteAsync(building.Id));
        using var _ = ActAs(user.Id);

        (await _usersAppService.GetMyBuildingAsync()).ShouldBeNull();
    }
}
