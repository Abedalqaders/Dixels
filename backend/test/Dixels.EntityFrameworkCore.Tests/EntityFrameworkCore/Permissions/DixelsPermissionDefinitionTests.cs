using System.Linq;
using System.Threading.Tasks;
using Dixels.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace Dixels.EntityFrameworkCore.Permissions;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class DixelsPermissionDefinitionTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;

    public DixelsPermissionDefinitionTests()
    {
        _permissionDefinitionManager = GetRequiredService<IPermissionDefinitionManager>();
    }

    [Theory]
    [InlineData(DixelsPermissions.Buildings.Default)]
    [InlineData(DixelsPermissions.Floors.Default)]
    [InlineData(DixelsPermissions.Spaces.Default)]
    [InlineData(DixelsPermissions.Overrides.Default)]
    [InlineData(DixelsPermissions.SpaceTypes.Default)]
    public async Task Each_Resource_Permission_Belongs_To_The_Dixels_Group(string permissionName)
    {
        var groups = await _permissionDefinitionManager.GetGroupsAsync();
        var dixelsGroup = groups.SingleOrDefault(g => g.Name == DixelsPermissions.GroupName);

        dixelsGroup.ShouldNotBeNull();
        dixelsGroup!.Permissions.ShouldContain(p => p.Name == permissionName);
    }

    [Theory]
    [InlineData(DixelsPermissions.Buildings.Create, DixelsPermissions.Buildings.Default)]
    [InlineData(DixelsPermissions.Buildings.Edit, DixelsPermissions.Buildings.Default)]
    [InlineData(DixelsPermissions.Buildings.Delete, DixelsPermissions.Buildings.Default)]
    [InlineData(DixelsPermissions.Floors.Create, DixelsPermissions.Floors.Default)]
    [InlineData(DixelsPermissions.Floors.Edit, DixelsPermissions.Floors.Default)]
    [InlineData(DixelsPermissions.Floors.Delete, DixelsPermissions.Floors.Default)]
    [InlineData(DixelsPermissions.Spaces.Create, DixelsPermissions.Spaces.Default)]
    [InlineData(DixelsPermissions.Spaces.Edit, DixelsPermissions.Spaces.Default)]
    [InlineData(DixelsPermissions.Spaces.Delete, DixelsPermissions.Spaces.Default)]
    [InlineData(DixelsPermissions.Overrides.Create, DixelsPermissions.Overrides.Default)]
    [InlineData(DixelsPermissions.Overrides.Delete, DixelsPermissions.Overrides.Default)]
    [InlineData(DixelsPermissions.SpaceTypes.Create, DixelsPermissions.SpaceTypes.Default)]
    [InlineData(DixelsPermissions.SpaceTypes.Edit, DixelsPermissions.SpaceTypes.Default)]
    [InlineData(DixelsPermissions.SpaceTypes.Delete, DixelsPermissions.SpaceTypes.Default)]
    public async Task Each_Action_Permission_Is_A_Child_Of_Its_Resource(string childName, string expectedParentName)
    {
        var permission = await _permissionDefinitionManager.GetOrNullAsync(childName);

        permission.ShouldNotBeNull();
        permission!.Parent.ShouldNotBeNull();
        permission.Parent!.Name.ShouldBe(expectedParentName);
    }

    [Fact]
    public async Task Overrides_Has_No_Edit_Permission()
    {
        // AvailabilityOverride is delete+recreate only, never edited in place — there's
        // deliberately no DixelsPermissions.Overrides.Edit to check against.
        var permission = await _permissionDefinitionManager.GetOrNullAsync(DixelsPermissions.Overrides.Default + ".Edit");

        permission.ShouldBeNull();
    }
}
