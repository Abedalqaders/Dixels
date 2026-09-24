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
public class SpacesAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly ISpacesAppService _spacesAppService;
    private readonly IFloorsAppService _floorsAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;

    public SpacesAppServiceTests()
    {
        _spacesAppService = GetRequiredService<ISpacesAppService>();
        _floorsAppService = GetRequiredService<IFloorsAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _spaceTypeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
    }

    private async Task<Floor> CreateFloorAsync(OperatingWindow? buildingHours = null)
    {
        var building = new Building(
            Guid.NewGuid(),
            "HQ",
            null,
            "UTC",
            OperatingDays.Everyday,
            buildingHours ?? OperatingWindow.FullDay,
            maxDurationMinutes: 120,
            maxHorizonDays: 30,
            minLeadMinutes: 0);
        await _buildingRepository.InsertAsync(building);

        var floor = new Floor(Guid.NewGuid(), building.Id, "Level 1", 1);
        return await _floorRepository.InsertAsync(floor);
    }

    private async Task<SpaceType> CreateSpaceTypeAsync() =>
        await _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "Desk-ish", IconKey.Desk));

    [Fact]
    public async Task Create_Then_Get_Defaults_To_Inherit_Everything()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();

        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Name = "Room A",
            SpaceTypeId = spaceType.Id,
            Capacity = 4,
        });

        var fetched = await _spacesAppService.GetAsync(created.Id);

        fetched.Capacity.ShouldBe(4);
        fetched.Days.ShouldBeNull();
        fetched.Hours.ShouldBeNull();
        fetched.MinAttendees.ShouldBeNull();
        fetched.HasOverrides.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_Changes_Identity_Fields_Including_SpaceType()
    {
        var floor = await CreateFloorAsync();
        var spaceType1 = await CreateSpaceTypeAsync();
        var spaceType2 = await _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "Meeting-ish", IconKey.MeetingRoom));

        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Name = "Room A",
            SpaceTypeId = spaceType1.Id,
            Capacity = 4,
        });

        var updated = await _spacesAppService.UpdateAsync(created.Id, new UpdateSpaceDto
        {
            Name = "Room B",
            SpaceTypeId = spaceType2.Id,
            Capacity = 6,
        });

        updated.Name.ShouldBe("Room B");
        updated.SpaceTypeId.ShouldBe(spaceType2.Id);
        updated.Capacity.ShouldBe(6);
    }

    [Fact]
    public async Task Lowering_Capacity_Below_MinAttendees_Throws_With_Both_Numbers()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Name = "Room A",
            SpaceTypeId = spaceType.Id,
            Capacity = 10,
        });

        await _spacesAppService.UpdateConstraintsAsync(created.Id, new UpdateSpaceConstraintsDto
        {
            MinAttendees = 6,
            ConcurrencyStamp = created.ConcurrencyStamp,
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _spacesAppService.UpdateAsync(created.Id, new UpdateSpaceDto
            {
                Name = "Room A",
                SpaceTypeId = spaceType.Id,
                Capacity = 5,
            }));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.CapacityBelowMinAttendees);
        exception.Data["capacity"].ShouldBe(5);
        exception.Data["minAttendees"].ShouldBe(6);
    }

    [Fact]
    public async Task MinAttendees_Above_Capacity_Throws()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Name = "Room A",
            SpaceTypeId = spaceType.Id,
            Capacity = 4,
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _spacesAppService.UpdateConstraintsAsync(created.Id, new UpdateSpaceConstraintsDto
            {
                MinAttendees = 5,
                ConcurrencyStamp = created.ConcurrencyStamp,
            }));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.MinAttendeesExceedsCapacity);
    }

    [Fact]
    public async Task UpdateConstraints_Validates_Against_Resolved_Floor_Value_Not_Raw_Floor_Row()
    {
        // Building is 24h, but the Floor narrows to 08:00-18:00 — the Space must be validated
        // against that resolved (Floor-override) value, not the Building's raw 24h row.
        // Set through the real FloorsAppService (not the entity directly), so the change is
        // actually flushed via its own SaveChangesAsync before SpacesAppService reads it back.
        var floor = await CreateFloorAsync();
        var floorDto = await _floorsAppService.GetAsync(floor.Id);
        await _floorsAppService.UpdateConstraintsAsync(floor.Id, new UpdateFloorConstraintsDto
        {
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "08:00", Close = "18:00" },
            ConcurrencyStamp = floorDto.ConcurrencyStamp,
        });

        var spaceType = await CreateSpaceTypeAsync();
        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Name = "Room A",
            SpaceTypeId = spaceType.Id,
            Capacity = 4,
        });

        // Fits inside the Floor's resolved 08:00-18:00 — must succeed.
        var ok = await _spacesAppService.UpdateConstraintsAsync(created.Id, new UpdateSpaceConstraintsDto
        {
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "09:00", Close = "17:00" },
            ConcurrencyStamp = created.ConcurrencyStamp,
        });
        ok.Warnings.ShouldBeEmpty();

        // Wider than the Floor's resolved hours (even though it would fit the Building's raw
        // 24h) — must be rejected.
        var refetched = await _spacesAppService.GetAsync(created.Id);
        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _spacesAppService.UpdateConstraintsAsync(created.Id, new UpdateSpaceConstraintsDto
            {
                Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "07:00", Close = "19:00" },
                ConcurrencyStamp = refetched.ConcurrencyStamp,
            }));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.HoursNotNarrower);
    }

    [Fact]
    public async Task UpdateConstraints_With_Stale_ConcurrencyStamp_Throws()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Name = "Room A",
            SpaceTypeId = spaceType.Id,
            Capacity = 4,
        });
        var staleStamp = created.ConcurrencyStamp;

        await _spacesAppService.UpdateConstraintsAsync(created.Id, new UpdateSpaceConstraintsDto
        {
            MaxDurationMinutes = 30,
            ConcurrencyStamp = staleStamp,
        });

        await Assert.ThrowsAsync<AbpDbConcurrencyException>(() =>
            _spacesAppService.UpdateConstraintsAsync(created.Id, new UpdateSpaceConstraintsDto
            {
                MaxDurationMinutes = 90,
                ConcurrencyStamp = staleStamp,
            }));
    }

    [Fact]
    public async Task GetResolvedConstraints_Includes_Building_And_Floor_Ancestor_Trail()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Name = "Room A",
            SpaceTypeId = spaceType.Id,
            Capacity = 4,
        });

        var resolved = await _spacesAppService.GetResolvedConstraintsAsync(created.Id);

        resolved.FloorId.ShouldBe(floor.Id);
        resolved.FloorName.ShouldBe("Level 1");
        resolved.BuildingId.ShouldBe(floor.BuildingId);
        resolved.Days.Source.ShouldBe("Building");
        resolved.Capacity.ShouldBe(4);
    }

    [Fact]
    public async Task Delete_Then_Restore_Roundtrips()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Name = "Room A",
            SpaceTypeId = spaceType.Id,
            Capacity = 4,
        });

        await _spacesAppService.DeleteAsync(created.Id);

        await Assert.ThrowsAnyAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() =>
            _spacesAppService.GetAsync(created.Id));

        await _spacesAppService.RestoreAsync(created.Id);

        var restored = await _spacesAppService.GetAsync(created.Id);
        restored.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetListAsync_Pages_And_Scopes_To_One_Floor()
    {
        var floorA = await CreateFloorAsync();
        var floorB = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();

        for (var i = 0; i < 5; i++)
        {
            await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floorA.Id, Name = $"Room {i:D2}", SpaceTypeId = spaceType.Id, Capacity = 4 });
        }
        await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floorB.Id, Name = "Other Floor Room", SpaceTypeId = spaceType.Id, Capacity = 4 });

        var page1 = await _spacesAppService.GetListAsync(new GetSpacesInput { FloorId = floorA.Id, SkipCount = 0, MaxResultCount = 2 });
        page1.TotalCount.ShouldBe(5);
        page1.Items.Count.ShouldBe(2);
        page1.Items.ShouldAllBe(s => s.FloorId == floorA.Id);

        var page2 = await _spacesAppService.GetListAsync(new GetSpacesInput { FloorId = floorA.Id, SkipCount = 2, MaxResultCount = 2 });
        page2.Items.Count.ShouldBe(2);
        page1.Items.Select(s => s.Id).ShouldNotContain(page2.Items[0].Id);
    }

    [Fact]
    public async Task GetListAsync_Filter_Narrows_By_Name()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floor.Id, Name = "Conference Room", SpaceTypeId = spaceType.Id, Capacity = 4 });
        await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floor.Id, Name = "Phone Booth", SpaceTypeId = spaceType.Id, Capacity = 1 });

        var result = await _spacesAppService.GetListAsync(new GetSpacesInput { FloorId = floor.Id, Filter = "Conference" });

        result.Items.ShouldContain(s => s.Name == "Conference Room");
        result.Items.ShouldNotContain(s => s.Name == "Phone Booth");
    }

    [Fact]
    public async Task GetListAsync_SpaceTypeId_Narrows_By_Type()
    {
        var floor = await CreateFloorAsync();
        var deskType = await CreateSpaceTypeAsync();
        var meetingType = await _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "Meeting-ish", IconKey.MeetingRoom));

        var desk = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floor.Id, Name = "Desk 1", SpaceTypeId = deskType.Id, Capacity = 1 });
        var meetingRoom = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floor.Id, Name = "Meeting Room 1", SpaceTypeId = meetingType.Id, Capacity = 8 });

        var result = await _spacesAppService.GetListAsync(new GetSpacesInput { FloorId = floor.Id, SpaceTypeId = deskType.Id });

        result.Items.Select(s => s.Id).ShouldContain(desk.Id);
        result.Items.Select(s => s.Id).ShouldNotContain(meetingRoom.Id);
    }

    [Fact]
    public async Task GetListAsync_Without_IncludeDeleted_Excludes_Deleted_Space()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floor.Id, Name = "Room A", SpaceTypeId = spaceType.Id, Capacity = 4 });
        await _spacesAppService.DeleteAsync(created.Id);

        var withoutDeleted = await _spacesAppService.GetListAsync(new GetSpacesInput { FloorId = floor.Id });
        withoutDeleted.Items.ShouldNotContain(s => s.Id == created.Id);

        var withDeleted = await _spacesAppService.GetListAsync(new GetSpacesInput { FloorId = floor.Id, IncludeDeleted = true });
        withDeleted.Items.Single(s => s.Id == created.Id).IsDeleted.ShouldBeTrue();
    }

    private async Task<Floor> CreateFloorAsync(string buildingName, string floorName)
    {
        var building = new Building(
            Guid.NewGuid(),
            buildingName,
            null,
            "UTC",
            OperatingDays.Everyday,
            OperatingWindow.FullDay,
            maxDurationMinutes: 120,
            maxHorizonDays: 30,
            minLeadMinutes: 0);
        await _buildingRepository.InsertAsync(building);

        var floor = new Floor(Guid.NewGuid(), building.Id, floorName, 1);
        return await _floorRepository.InsertAsync(floor);
    }

    [Fact]
    public async Task GetListAsync_Without_FloorId_Lists_Spaces_Across_Every_Floor_With_FloorName_And_BuildingName()
    {
        var floorA = await CreateFloorAsync("Building A", "Level 1");
        var floorB = await CreateFloorAsync("Building B", "Level 2");
        var spaceType = await CreateSpaceTypeAsync();

        var spaceA = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floorA.Id, Name = "Room A", SpaceTypeId = spaceType.Id, Capacity = 4 });
        var spaceB = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = floorB.Id, Name = "Room B", SpaceTypeId = spaceType.Id, Capacity = 4 });

        var result = await _spacesAppService.GetListAsync(new GetSpacesInput { MaxResultCount = 100 });

        result.Items.Single(s => s.Id == spaceA.Id).FloorName.ShouldBe("Level 1");
        result.Items.Single(s => s.Id == spaceA.Id).BuildingName.ShouldBe("Building A");
        result.Items.Single(s => s.Id == spaceB.Id).FloorName.ShouldBe("Level 2");
        result.Items.Single(s => s.Id == spaceB.Id).BuildingName.ShouldBe("Building B");
    }

    [Fact]
    public async Task GetListAsync_Filter_Also_Matches_Against_Floor_And_Building_Names()
    {
        var matchingFloor = await CreateFloorAsync("Some Building", "Riverside Level");
        var otherFloor = await CreateFloorAsync("Riverside Tower", "Some Level");
        var unrelatedFloor = await CreateFloorAsync("Other Building", "Other Level");
        var spaceType = await CreateSpaceTypeAsync();

        var spaceOnMatchingFloor = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = matchingFloor.Id, Name = "Room A", SpaceTypeId = spaceType.Id, Capacity = 4 });
        var spaceInMatchingBuilding = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = otherFloor.Id, Name = "Room B", SpaceTypeId = spaceType.Id, Capacity = 4 });
        var unrelatedSpace = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = unrelatedFloor.Id, Name = "Room C", SpaceTypeId = spaceType.Id, Capacity = 4 });

        var result = await _spacesAppService.GetListAsync(new GetSpacesInput { Filter = "Riverside" });

        result.Items.Select(s => s.Id).ShouldContain(spaceOnMatchingFloor.Id);
        result.Items.Select(s => s.Id).ShouldContain(spaceInMatchingBuilding.Id);
        result.Items.Select(s => s.Id).ShouldNotContain(unrelatedSpace.Id);
    }

    [Fact]
    public async Task GetListAsync_BuildingId_Narrows_Across_All_Its_Floors()
    {
        var building = await CreateFloorAsync("Target Building", "Level 1");
        var secondFloorInSameBuilding = new Floor(Guid.NewGuid(), building.BuildingId, "Level 2", 2);
        await _floorRepository.InsertAsync(secondFloorInSameBuilding);
        var otherBuildingFloor = await CreateFloorAsync("Other Building", "Level 1");
        var spaceType = await CreateSpaceTypeAsync();

        var spaceOnFloor1 = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = building.Id, Name = "Room A", SpaceTypeId = spaceType.Id, Capacity = 4 });
        var spaceOnFloor2 = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = secondFloorInSameBuilding.Id, Name = "Room B", SpaceTypeId = spaceType.Id, Capacity = 4 });
        var spaceInOtherBuilding = await _spacesAppService.CreateAsync(new CreateSpaceDto { FloorId = otherBuildingFloor.Id, Name = "Room C", SpaceTypeId = spaceType.Id, Capacity = 4 });

        var result = await _spacesAppService.GetListAsync(new GetSpacesInput { BuildingId = building.BuildingId });

        result.Items.Select(s => s.Id).ShouldContain(spaceOnFloor1.Id);
        result.Items.Select(s => s.Id).ShouldContain(spaceOnFloor2.Id);
        result.Items.Select(s => s.Id).ShouldNotContain(spaceInOtherBuilding.Id);
    }
}
