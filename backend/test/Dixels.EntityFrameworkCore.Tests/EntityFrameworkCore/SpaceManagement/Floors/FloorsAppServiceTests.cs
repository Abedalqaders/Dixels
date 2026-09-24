using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class FloorsAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IFloorsAppService _floorsAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;

    public FloorsAppServiceTests()
    {
        _floorsAppService = GetRequiredService<IFloorsAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
        _spaceTypeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
    }

    private async Task<Building> CreateBuildingAsync(string name = "HQ")
    {
        var building = new Building(
            Guid.NewGuid(),
            name,
            null,
            "UTC",
            OperatingDays.Everyday,
            OperatingWindow.FullDay,
            maxDurationMinutes: 120,
            maxHorizonDays: 30,
            minLeadMinutes: 0);
        return await _buildingRepository.InsertAsync(building);
    }

    [Fact]
    public async Task Create_Then_Get_Defaults_To_Inherit_Everything()
    {
        var building = await CreateBuildingAsync();

        var created = await _floorsAppService.CreateAsync(new CreateFloorDto
        {
            BuildingId = building.Id,
            Name = "Level 1",
            FloorNumber = 1,
        });

        var fetched = await _floorsAppService.GetAsync(created.Id);

        fetched.Days.ShouldBeNull();
        fetched.Hours.ShouldBeNull();
        fetched.MaxDurationMinutes.ShouldBeNull();
        fetched.HasOverrides.ShouldBeFalse();
        fetched.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Update_Changes_Identity_Fields_Only()
    {
        var building = await CreateBuildingAsync();
        var created = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Old" });

        var updated = await _floorsAppService.UpdateAsync(created.Id, new UpdateFloorDto { Name = "New", FloorNumber = 5 });

        updated.Name.ShouldBe("New");
        updated.FloorNumber.ShouldBe(5);
    }

    [Fact]
    public async Task UpdateConstraints_Sets_An_Override_And_Marks_HasOverrides()
    {
        var building = await CreateBuildingAsync();
        var created = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });

        var result = await _floorsAppService.UpdateConstraintsAsync(created.Id, new UpdateFloorConstraintsDto
        {
            Days = new[] { 1, 2, 3, 4, 5 },
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "08:00", Close = "18:00" },
            MaxDurationMinutes = 60,
            ConcurrencyStamp = created.ConcurrencyStamp,
        });

        result.Warnings.ShouldBeEmpty();

        var fetched = await _floorsAppService.GetAsync(created.Id);
        fetched.HasOverrides.ShouldBeTrue();
        fetched.Days!.OrderBy(d => d).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        fetched.Hours!.Open.ShouldBe("08:00");
    }

    [Fact]
    public async Task UpdateConstraints_Clearing_Back_To_Null_Restores_Inherit()
    {
        var building = await CreateBuildingAsync();
        var created = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });

        var first = await _floorsAppService.UpdateConstraintsAsync(created.Id, new UpdateFloorConstraintsDto
        {
            Days = new[] { 1, 2, 3 },
            Hours = null,
            MaxDurationMinutes = 45,
            ConcurrencyStamp = created.ConcurrencyStamp,
        });

        var cleared = await _floorsAppService.UpdateConstraintsAsync(created.Id, new UpdateFloorConstraintsDto
        {
            Days = null,
            Hours = null,
            MaxDurationMinutes = null,
            ConcurrencyStamp = first.ConcurrencyStamp,
        });

        cleared.Warnings.ShouldBeEmpty();
        var fetched = await _floorsAppService.GetAsync(created.Id);
        fetched.HasOverrides.ShouldBeFalse();
        fetched.Days.ShouldBeNull();
        fetched.MaxDurationMinutes.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateConstraints_Wider_Than_Building_Throws()
    {
        var building = new Building(
            Guid.NewGuid(),
            "Narrow Hours HQ",
            null,
            "UTC",
            OperatingDays.Everyday,
            OperatingWindow.Create(new TimeOnly(8, 0), new TimeOnly(18, 0)),
            maxDurationMinutes: 60,
            maxHorizonDays: 30,
            minLeadMinutes: 0);
        await _buildingRepository.InsertAsync(building);

        var created = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _floorsAppService.UpdateConstraintsAsync(created.Id, new UpdateFloorConstraintsDto
            {
                Days = null,
                Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "07:00", Close = "20:00" },
                MaxDurationMinutes = null,
                ConcurrencyStamp = created.ConcurrencyStamp,
            }));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.HoursNotNarrower);
    }

    [Fact]
    public async Task UpdateConstraints_With_Stale_ConcurrencyStamp_Throws()
    {
        var building = await CreateBuildingAsync();
        var created = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });
        var staleStamp = created.ConcurrencyStamp;

        await _floorsAppService.UpdateConstraintsAsync(created.Id, new UpdateFloorConstraintsDto
        {
            MaxDurationMinutes = 30,
            ConcurrencyStamp = staleStamp,
        });

        await Assert.ThrowsAsync<AbpDbConcurrencyException>(() =>
            _floorsAppService.UpdateConstraintsAsync(created.Id, new UpdateFloorConstraintsDto
            {
                MaxDurationMinutes = 90,
                ConcurrencyStamp = staleStamp,
            }));
    }

    [Fact]
    public async Task Tightening_Floor_Hours_Over_An_Existing_Space_Override_Returns_A_Warning()
    {
        var building = await CreateBuildingAsync();
        var floor = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });
        var spaceType = await _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "Desk-ish", IconKey.Desk));

        var space = new Space(Guid.NewGuid(), floor.Id, "Room A", spaceType.Id, capacity: 4);
        space.SetOwnOperatingHours(
            OperatingWindow.Create(new TimeOnly(20, 0), new TimeOnly(23, 0)),
            OperatingWindow.FullDay);
        await _spaceRepository.InsertAsync(space);

        var result = await _floorsAppService.UpdateConstraintsAsync(floor.Id, new UpdateFloorConstraintsDto
        {
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "06:00", Close = "22:00" },
            ConcurrencyStamp = floor.ConcurrencyStamp,
        });

        result.Warnings.ShouldContain(w => w.Contains("Room A"));
    }

    [Fact]
    public async Task GetResolvedConstraints_Includes_Building_Ancestor_Trail()
    {
        var building = await CreateBuildingAsync("Resolved HQ");
        var floor = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });

        var resolved = await _floorsAppService.GetResolvedConstraintsAsync(floor.Id);

        resolved.BuildingId.ShouldBe(building.Id);
        resolved.BuildingName.ShouldBe("Resolved HQ");
        resolved.Days.Source.ShouldBe("Building");
        resolved.Hours.Source.ShouldBe("Building");
    }

    [Fact]
    public async Task Delete_Cascades_To_Spaces_Then_Restore_Brings_Them_Back()
    {
        var building = await CreateBuildingAsync();
        var floor = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });
        var spaceType = await _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "Desk-ish", IconKey.Desk));

        var space = new Space(Guid.NewGuid(), floor.Id, "Room A", spaceType.Id, capacity: 4);
        await _spaceRepository.InsertAsync(space);

        await _floorsAppService.DeleteAsync(floor.Id);

        (await _spaceRepository.FindAsync(space.Id)).ShouldBeNull();

        await _floorsAppService.RestoreAsync(floor.Id);

        var restoredFloor = await _floorsAppService.GetAsync(floor.Id);
        restoredFloor.ShouldNotBeNull();

        var restoredSpace = await _spaceRepository.FindAsync(space.Id);
        restoredSpace.ShouldNotBeNull();
        restoredSpace!.DeletionBatchId.ShouldBeNull();
    }

    [Fact]
    public async Task Restore_Does_Not_Resurrect_A_Space_Deleted_Independently_Earlier()
    {
        var building = await CreateBuildingAsync();
        var floor = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });
        var spaceType = await _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "Desk-ish", IconKey.Desk));

        var independentlyDeletedSpace = new Space(Guid.NewGuid(), floor.Id, "Already Gone", spaceType.Id, capacity: 2);
        await _spaceRepository.InsertAsync(independentlyDeletedSpace);
        await _spaceRepository.DeleteAsync(independentlyDeletedSpace);

        var laterSpace = new Space(Guid.NewGuid(), floor.Id, "Still Here Until Delete", spaceType.Id, capacity: 4);
        await _spaceRepository.InsertAsync(laterSpace);

        await _floorsAppService.DeleteAsync(floor.Id);
        await _floorsAppService.RestoreAsync(floor.Id);

        (await _spaceRepository.FindAsync(laterSpace.Id)).ShouldNotBeNull();
        (await _spaceRepository.FindAsync(independentlyDeletedSpace.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task GetListAsync_Pages_And_Scopes_To_One_Building()
    {
        var buildingA = await CreateBuildingAsync("Building A");
        var buildingB = await CreateBuildingAsync("Building B");

        for (var i = 0; i < 5; i++)
        {
            await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = buildingA.Id, Name = $"Floor {i:D2}" });
        }
        await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = buildingB.Id, Name = "Other Building Floor" });

        var page1 = await _floorsAppService.GetListAsync(new GetFloorsInput { BuildingId = buildingA.Id, SkipCount = 0, MaxResultCount = 2 });
        page1.TotalCount.ShouldBe(5);
        page1.Items.Count.ShouldBe(2);
        page1.Items.ShouldAllBe(f => f.BuildingId == buildingA.Id);

        var page2 = await _floorsAppService.GetListAsync(new GetFloorsInput { BuildingId = buildingA.Id, SkipCount = 2, MaxResultCount = 2 });
        page2.Items.Count.ShouldBe(2);
        page1.Items.Select(f => f.Id).ShouldNotContain(page2.Items[0].Id);
    }

    [Fact]
    public async Task GetListAsync_Filter_Narrows_By_Name()
    {
        var building = await CreateBuildingAsync();
        await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Ground Floor" });
        await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Mezzanine" });

        var result = await _floorsAppService.GetListAsync(new GetFloorsInput { BuildingId = building.Id, Filter = "Ground" });

        result.Items.ShouldContain(f => f.Name == "Ground Floor");
        result.Items.ShouldNotContain(f => f.Name == "Mezzanine");
    }

    [Fact]
    public async Task GetListAsync_Without_IncludeDeleted_Excludes_Deleted_Floor()
    {
        var building = await CreateBuildingAsync();
        var floor = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Name = "Level 1" });
        await _floorsAppService.DeleteAsync(floor.Id);

        var withoutDeleted = await _floorsAppService.GetListAsync(new GetFloorsInput { BuildingId = building.Id });
        withoutDeleted.Items.ShouldNotContain(f => f.Id == floor.Id);

        var withDeleted = await _floorsAppService.GetListAsync(new GetFloorsInput { BuildingId = building.Id, IncludeDeleted = true });
        withDeleted.Items.Single(f => f.Id == floor.Id).IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_Without_BuildingId_Lists_Floors_Across_Every_Building_With_BuildingName()
    {
        var buildingA = await CreateBuildingAsync("Building A");
        var buildingB = await CreateBuildingAsync("Building B");
        var floorA = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = buildingA.Id, Name = "Level 1" });
        var floorB = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = buildingB.Id, Name = "Level 1" });

        var result = await _floorsAppService.GetListAsync(new GetFloorsInput { MaxResultCount = 100 });

        result.Items.Select(f => f.Id).ShouldContain(floorA.Id);
        result.Items.Select(f => f.Id).ShouldContain(floorB.Id);
        result.Items.Single(f => f.Id == floorA.Id).BuildingName.ShouldBe("Building A");
        result.Items.Single(f => f.Id == floorB.Id).BuildingName.ShouldBe("Building B");
    }

    [Fact]
    public async Task GetListAsync_Filter_Also_Matches_Against_The_Building_Name()
    {
        var matchingBuilding = await CreateBuildingAsync("Riverside Tower");
        var otherBuilding = await CreateBuildingAsync("Lakeside Tower");
        var floorInMatchingBuilding = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = matchingBuilding.Id, Name = "Level 1" });
        var floorInOtherBuilding = await _floorsAppService.CreateAsync(new CreateFloorDto { BuildingId = otherBuilding.Id, Name = "Level 1" });

        var result = await _floorsAppService.GetListAsync(new GetFloorsInput { Filter = "Riverside" });

        result.Items.Select(f => f.Id).ShouldContain(floorInMatchingBuilding.Id);
        result.Items.Select(f => f.Id).ShouldNotContain(floorInOtherBuilding.Id);
    }
}
