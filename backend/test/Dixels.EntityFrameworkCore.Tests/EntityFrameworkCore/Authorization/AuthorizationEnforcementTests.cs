using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Xunit;
using static Dixels.TestNames;

namespace Dixels.EntityFrameworkCore.Authorization;

/// <summary>
/// What each kind of role is refused, with authorization really switched on
/// (<see cref="DixelsAuthorizationTestModule"/>). "See the path, act only where granted":
/// reading the levels above your own is allowed, changing anything still needs its own
/// permission. No data is set up — an allowed call on an id that doesn't exist fails with
/// <em>not found</em> (or any other non-authorization error), which is proof enough that
/// authorization let it through; a refused one never gets that far.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class AuthorizationEnforcementTests : DixelsApplicationTestBase<DixelsAuthorizationTestModule>
{
    private readonly FakePermissionChecker _permissions;
    private readonly IBuildingsAppService _buildings;
    private readonly IFloorsAppService _floors;
    private readonly ISpacesAppService _spaces;
    private readonly ISpaceTypesAppService _spaceTypes;
    private readonly IAvailabilityOverridesAppService _overrides;
    private readonly IBookingsAppService _bookings;
    private readonly IIdentityUserAppService _users;

    public AuthorizationEnforcementTests()
    {
        _permissions = GetRequiredService<FakePermissionChecker>();
        _buildings = GetRequiredService<IBuildingsAppService>();
        _floors = GetRequiredService<IFloorsAppService>();
        _spaces = GetRequiredService<ISpacesAppService>();
        _spaceTypes = GetRequiredService<ISpaceTypesAppService>();
        _overrides = GetRequiredService<IAvailabilityOverridesAppService>();
        _bookings = GetRequiredService<IBookingsAppService>();
        _users = GetRequiredService<IIdentityUserAppService>();
    }

    private void ActAs(params string[] grants) => _permissions.Grant(grants);

    private static async Task<Exception?> Outcome(Func<Task> call)
    {
        try
        {
            await call();
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    /// <summary>The call was refused before it touched anything.</summary>
    private static async Task ShouldRefuse(Func<Task> call)
    {
        var outcome = await Outcome(call);
        outcome.ShouldNotBeNull("expected the call to be refused, but it succeeded");
        outcome.ShouldBeAssignableTo<AbpAuthorizationException>($"expected an authorization refusal, got {outcome.GetType().Name}: {outcome.Message}");
    }

    /// <summary>Authorization let the call through — whatever it then did with a made-up id.</summary>
    private static async Task ShouldAllow(Func<Task> call)
    {
        var outcome = await Outcome(call);
        if (outcome is not null)
        {
            outcome.ShouldNotBeAssignableTo<AbpAuthorizationException>($"expected the call to be allowed, but it was refused: {outcome.Message}");
        }
    }

    private static readonly Guid Missing = Guid.NewGuid();

    [Fact]
    public async Task A_floor_editor_sees_the_buildings_above_but_may_only_change_floors()
    {
        ActAs(DixelsPermissions.Floors.Default, DixelsPermissions.Floors.Edit);

        await ShouldAllow(() => _buildings.GetListAsync(new GetBuildingsInput()));
        await ShouldAllow(() => _buildings.GetAsync(Missing));
        await ShouldRefuse(() => _buildings.RestoreAsync(Missing)); // Buildings.Edit
        await ShouldRefuse(() => _buildings.DeleteAsync(Missing)); // Buildings.Delete

        await ShouldAllow(() => _floors.GetListAsync(new GetFloorsInput()));
        await ShouldAllow(() => _floors.RestoreAsync(Missing)); // Floors.Edit — passes, then "not found"
        await ShouldAllow(() => _floors.GetResolvedConstraintsAsync(Missing));
        await ShouldRefuse(() => _floors.DeleteAsync(Missing)); // Floors.Delete

        await ShouldRefuse(() => _spaces.GetListAsync(new GetSpacesInput()));
        await ShouldRefuse(() => _overrides.DeleteAsync(Missing));
    }

    [Fact]
    public async Task A_space_viewer_walks_the_whole_path_down_read_only()
    {
        ActAs(DixelsPermissions.Spaces.Default);

        await ShouldAllow(() => _buildings.GetListAsync(new GetBuildingsInput()));
        await ShouldAllow(() => _floors.GetListAsync(new GetFloorsInput()));
        await ShouldAllow(() => _floors.GetAsync(Missing));
        await ShouldAllow(() => _spaceTypes.GetListAsync(new GetSpaceTypesInput())); // every space names one
        await ShouldAllow(() => _spaces.GetListAsync(new GetSpacesInput()));

        await ShouldRefuse(() => _floors.GetResolvedConstraintsAsync(Missing)); // a floor's own rules: Floors.Default
        await ShouldRefuse(() => _spaces.RestoreAsync(Missing)); // Spaces.Edit
        await ShouldRefuse(() => _spaces.DeleteAsync(Missing)); // Spaces.Delete
        await ShouldRefuse(() => _spaceTypes.DeleteAsync(Missing)); // SpaceTypes.Delete
        await ShouldRefuse(() => _overrides.GetListAsync(OverrideScope.Space, Missing)); // Overrides.Default
    }

    [Fact]
    public async Task A_building_only_viewer_is_not_shown_floors()
    {
        ActAs(DixelsPermissions.Buildings.Default);

        await ShouldAllow(() => _buildings.GetListAsync(new GetBuildingsInput()));
        await ShouldRefuse(() => _floors.GetListAsync(new GetFloorsInput()));
        await ShouldRefuse(() => _spaceTypes.GetListAsync(new GetSpaceTypesInput()));
    }

    [Fact]
    public async Task A_closures_coordinator_changes_closures_but_not_the_rules()
    {
        ActAs(DixelsPermissions.Spaces.Default, DixelsPermissions.Overrides.Default, DixelsPermissions.Overrides.Create, DixelsPermissions.Overrides.Delete);

        await ShouldAllow(() => _overrides.GetListAsync(OverrideScope.Space, Missing));
        await ShouldAllow(() => _overrides.DeleteAsync(Missing));
        await ShouldRefuse(() => _spaces.RestoreAsync(Missing)); // the rules themselves: Spaces.Edit
    }

    [Fact]
    public async Task A_user_admin_reads_buildings_for_the_picker_and_nothing_else_in_the_hierarchy()
    {
        ActAs("AbpIdentity.Users", "AbpIdentity.Users.Update");

        await ShouldAllow(() => _users.GetListAsync(new GetIdentityUsersInput()));
        await ShouldAllow(() => _buildings.GetListAsync(new GetBuildingsInput()));
        await ShouldAllow(() => _buildings.GetAsync(Missing));

        await ShouldRefuse(() => _buildings.RestoreAsync(Missing));
        await ShouldRefuse(() => _floors.GetListAsync(new GetFloorsInput()));
        await ShouldRefuse(() => _spaceTypes.GetListAsync(new GetSpaceTypesInput()));
    }

    [Fact]
    public async Task An_employee_without_cancel_still_sees_bookings_but_cannot_cancel()
    {
        ActAs(DixelsPermissions.Bookings.Default, DixelsPermissions.Bookings.Create);

        await ShouldAllow(() => _bookings.GetAsync(Missing));
        await ShouldRefuse(() => _bookings.CancelAsync(Missing, new CancelBookingDto()));

        ActAs(DixelsPermissions.Bookings.Default, DixelsPermissions.Bookings.Cancel);

        await ShouldAllow(() => _bookings.CancelAsync(Missing, new CancelBookingDto()));
        await ShouldRefuse(() => _buildings.GetListAsync(new GetBuildingsInput()));
    }

    [Fact]
    public async Task Someone_granted_nothing_is_refused_everywhere_with_abps_own_error_code()
    {
        ActAs();

        await ShouldRefuse(() => _buildings.GetListAsync(new GetBuildingsInput()));
        await ShouldRefuse(() => _floors.GetListAsync(new GetFloorsInput()));
        await ShouldRefuse(() => _spaceTypes.GetListAsync(new GetSpaceTypesInput()));
        await ShouldRefuse(() => _bookings.GetAsync(Missing));
        await ShouldRefuse(() => _users.GetListAsync(new GetIdentityUsersInput()));

        // The reader rule refuses through the same door as [Authorize]: the frontend tells a
        // missing permission from a business error by this code prefix (Volo.Authorization:).
        var refusal = (AbpAuthorizationException?)await Outcome(() => _buildings.GetListAsync(new GetBuildingsInput()));
        refusal.ShouldNotBeNull();
        refusal.Code.ShouldBe(AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted);
    }

    // A role granted only Create (or only Delete) at a level must be able to do exactly that:
    // the internal "can manage this building" seam must not demand Edit on top.
    [Fact]
    public async Task Create_and_delete_need_only_their_own_permission()
    {
        ActAs(DixelsPermissions.Spaces.Default, DixelsPermissions.Spaces.Create);
        await ShouldAllow(() => _spaces.CreateAsync(new CreateSpaceDto { FloorId = Missing, Names = En("Desk"), SpaceTypeId = Missing, Capacity = 1 }));

        ActAs(DixelsPermissions.Spaces.Default, DixelsPermissions.Spaces.Delete);
        await ShouldAllow(() => _spaces.DeleteAsync(Missing));

        ActAs(DixelsPermissions.Floors.Create);
        await ShouldAllow(() => _floors.CreateAsync(new CreateFloorDto { BuildingId = Missing, Names = En("Level 1") }));

        ActAs(DixelsPermissions.Floors.Delete);
        await ShouldAllow(() => _floors.DeleteAsync(Missing));

        ActAs(DixelsPermissions.Buildings.Delete);
        await ShouldAllow(() => _buildings.DeleteAsync(Missing));
    }
}
