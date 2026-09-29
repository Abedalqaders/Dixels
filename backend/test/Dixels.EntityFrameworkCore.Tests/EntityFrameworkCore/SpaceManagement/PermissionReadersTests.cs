using Dixels.Permissions;
using Shouldly;
using Xunit;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

/// <summary>
/// "See the path, act only where granted": whoever may see a level can list the levels above
/// it (read-only) to reach it. The frontend mirrors these lists (permissionNames.ts), so a
/// change here that isn't made there would show pages the API then refuses, or the reverse.
/// </summary>
public class PermissionReadersTests
{
    [Fact]
    public void Buildings_can_be_read_by_anyone_below_them_in_the_tree_and_by_user_admins()
    {
        DixelsPermissions.Readers.Buildings.ShouldBe(new[]
        {
            DixelsPermissions.Buildings.Default,
            DixelsPermissions.Floors.Default,
            DixelsPermissions.Spaces.Default,
            "AbpIdentity.Users",
        });
    }

    [Fact]
    public void Floors_can_be_read_by_space_viewers_too()
    {
        DixelsPermissions.Readers.Floors.ShouldBe(new[] { DixelsPermissions.Floors.Default, DixelsPermissions.Spaces.Default });
    }

    [Fact]
    public void Space_types_can_be_read_by_space_viewers_too()
    {
        DixelsPermissions.Readers.SpaceTypes.ShouldBe(new[] { DixelsPermissions.SpaceTypes.Default, DixelsPermissions.Spaces.Default });
    }

    [Fact]
    public void Readers_never_include_a_write_permission()
    {
        // Reading the path is free; changing anything on it never is.
        foreach (var name in DixelsPermissions.Readers.Buildings)
        {
            name.ShouldNotEndWith(".Create");
            name.ShouldNotEndWith(".Edit");
            name.ShouldNotEndWith(".Delete");
        }
    }
}
