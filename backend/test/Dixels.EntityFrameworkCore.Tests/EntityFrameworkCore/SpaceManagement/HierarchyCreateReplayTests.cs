using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;
using static Dixels.TestNames;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

/// <summary>
/// An admin's "add building / floor / space" sent twice with the same key (a double-click, a
/// retry after a lost answer) makes one, and the second request gets that one back.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class HierarchyCreateReplayTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBuildingsAppService _buildingsAppService;
    private readonly IFloorsAppService _floorsAppService;
    private readonly ISpacesAppService _spacesAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public HierarchyCreateReplayTests()
    {
        _buildingsAppService = GetRequiredService<IBuildingsAppService>();
        _floorsAppService = GetRequiredService<IFloorsAppService>();
        _spacesAppService = GetRequiredService<ISpacesAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
        _spaceTypeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private async Task<Building> CreateBuildingAsync() =>
        await _buildingRepository.InsertAsync(new Building(
            Guid.NewGuid(), "en", "HQ", null, "UTC", OperatingDays.Everyday, OperatingWindow.FullDay,
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));

    private static CreateBuildingDto NewBuilding(string name, string? key) => new()
    {
        Names = En(name),
        Timezone = "UTC",
        Days = [0, 1, 2, 3, 4, 5, 6],
        Hours = new OperatingWindowDto { IsOpen24Hours = true },
        MaxDurationMinutes = 120,
        MaxHorizonDays = 30,
        IdempotencyKey = key,
    };

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private async Task<int> FloorsInAsync(Guid buildingId) => (await _floorRepository.GetListAsync(f => f.BuildingId == buildingId)).Count;

    [Fact]
    public async Task A_floor_sent_twice_with_its_key_is_made_once()
    {
        var building = await CreateBuildingAsync();
        var request = new CreateFloorDto { BuildingId = building.Id, Names = En("Level 1"), FloorNumber = 1, IdempotencyKey = "floor-key" };

        var first = await _floorsAppService.CreateAsync(request);
        var again = await _floorsAppService.CreateAsync(request);

        again.Id.ShouldBe(first.Id);
        (await FloorsInAsync(building.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task A_space_sent_twice_with_its_key_is_made_once()
    {
        var building = await CreateBuildingAsync();
        var floor = await _floorRepository.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var spaceType = await _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "en", "Room-ish", IconKey.Desk));
        var request = new CreateSpaceDto { FloorId = floor.Id, Names = En("Room A"), SpaceTypeId = spaceType.Id, Capacity = 4, IdempotencyKey = "space-key" };

        var first = await _spacesAppService.CreateAsync(request);
        var again = await _spacesAppService.CreateAsync(request);

        again.Id.ShouldBe(first.Id);
        (await _spaceRepository.GetListAsync(s => s.FloorId == floor.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_building_sent_twice_with_its_key_is_made_once()
    {
        var first = await _buildingsAppService.CreateAsync(NewBuilding("Riverside", "building-key"));
        var again = await _buildingsAppService.CreateAsync(NewBuilding("Riverside", "building-key"));

        again.Id.ShouldBe(first.Id);
        (await _buildingRepository.GetListAsync(b => b.IdempotencyKey == "building-key")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_same_key_with_different_details_is_refused()
    {
        var building = await CreateBuildingAsync();
        await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Names = En("Level 1"), IdempotencyKey = "reused" });

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Names = En("Level 2"), IdempotencyKey = "reused" }));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.HierarchyIdempotencyKeyReused);
        (await FloorsInAsync(building.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task A_retry_after_the_floor_was_deleted_is_refused_not_made_again()
    {
        var building = await CreateBuildingAsync();
        var request = new CreateFloorDto { BuildingId = building.Id, Names = En("Level 1"), IdempotencyKey = "deleted" };
        var first = await _floorsAppService.CreateAsync(request);
        await _floorsAppService.DeleteAsync(first.Id);

        var ex = await Should.ThrowAsync<BusinessException>(() => _floorsAppService.CreateAsync(request));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.HierarchyIdempotencyKeyReused);
        (await FloorsInAsync(building.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Without_a_key_every_add_makes_a_new_one()
    {
        var building = await CreateBuildingAsync();
        var request = new CreateFloorDto { BuildingId = building.Id, Names = En("Level 1") };

        var first = await _floorsAppService.CreateAsync(request);
        var second = await _floorsAppService.CreateAsync(request);

        second.Id.ShouldNotBe(first.Id);
        (await FloorsInAsync(building.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task Another_admins_same_key_is_their_own()
    {
        BuildingDto mine, theirs;
        using (ActAs(Guid.NewGuid()))
        {
            mine = await _buildingsAppService.CreateAsync(NewBuilding("North", "shared-key"));
        }

        using (ActAs(Guid.NewGuid()))
        {
            theirs = await _buildingsAppService.CreateAsync(NewBuilding("North", "shared-key"));
        }

        theirs.Id.ShouldNotBe(mine.Id);
    }
}
