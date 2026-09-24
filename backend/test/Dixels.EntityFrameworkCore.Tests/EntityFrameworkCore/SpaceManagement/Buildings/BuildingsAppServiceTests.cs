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
public class BuildingsAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBuildingsAppService _buildingsAppService;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;

    public BuildingsAppServiceTests()
    {
        _buildingsAppService = GetRequiredService<IBuildingsAppService>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
        _spaceTypeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
    }

    private static CreateBuildingDto NewBuildingInput(string name = "HQ") => new()
    {
        Name = name,
        Timezone = "UTC",
        Days = new[] { 0, 1, 2, 3, 4, 5, 6 },
        Hours = new OperatingWindowDto { IsOpen24Hours = true },
        MaxDurationMinutes = 120,
        MaxHorizonDays = 30,
        MinLeadMinutes = 0,
    };

    [Fact]
    public async Task Create_Then_Get_Roundtrips_Days_And_Hours()
    {
        var created = await _buildingsAppService.CreateAsync(new CreateBuildingDto
        {
            Name = "Main Building",
            Timezone = "Europe/London",
            Days = new[] { 1, 2, 3, 4, 5 },
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "08:00", Close = "18:00" },
            MaxDurationMinutes = 60,
            MaxHorizonDays = 14,
            MinLeadMinutes = 15,
        });

        var fetched = await _buildingsAppService.GetAsync(created.Id);

        fetched.Name.ShouldBe("Main Building");
        fetched.Days.OrderBy(d => d).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        fetched.Hours.IsOpen24Hours.ShouldBeFalse();
        fetched.Hours.Open.ShouldBe("08:00");
        fetched.Hours.Close.ShouldBe("18:00");
        fetched.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Update_Changes_Identity_Fields_Only()
    {
        var created = await _buildingsAppService.CreateAsync(NewBuildingInput());

        var updated = await _buildingsAppService.UpdateAsync(created.Id, new UpdateBuildingDto
        {
            Name = "Renamed HQ",
            BuildingNumber = "12B",
            Timezone = "America/New_York",
        });

        updated.Name.ShouldBe("Renamed HQ");
        updated.BuildingNumber.ShouldBe("12B");
        updated.Timezone.ShouldBe("America/New_York");
    }

    [Fact]
    public async Task UpdateConstraints_Is_One_Atomic_Save_And_Returns_Fresh_Stamp()
    {
        var created = await _buildingsAppService.CreateAsync(NewBuildingInput());

        var result = await _buildingsAppService.UpdateConstraintsAsync(created.Id, new UpdateBuildingConstraintsDto
        {
            Days = new[] { 1, 2, 3, 4, 5 },
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "09:00", Close = "17:00" },
            MaxDurationMinutes = 90,
            MaxHorizonDays = 60,
            MinLeadMinutes = 30,
            ConcurrencyStamp = created.ConcurrencyStamp,
        });

        result.Warnings.ShouldBeEmpty();
        result.ConcurrencyStamp.ShouldNotBe(created.ConcurrencyStamp);

        var fetched = await _buildingsAppService.GetAsync(created.Id);
        fetched.Days.OrderBy(d => d).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        fetched.MaxDurationMinutes.ShouldBe(90);
    }

    [Fact]
    public async Task UpdateConstraints_With_Stale_ConcurrencyStamp_Throws()
    {
        var created = await _buildingsAppService.CreateAsync(NewBuildingInput());
        var staleStamp = created.ConcurrencyStamp;

        // First save succeeds and moves the stamp forward.
        await _buildingsAppService.UpdateConstraintsAsync(created.Id, new UpdateBuildingConstraintsDto
        {
            Days = created.Days,
            Hours = created.Hours,
            MaxDurationMinutes = created.MaxDurationMinutes,
            MaxHorizonDays = created.MaxHorizonDays,
            MinLeadMinutes = created.MinLeadMinutes,
            ConcurrencyStamp = staleStamp,
        });

        // Second save reuses the now-stale stamp — must be rejected, not silently overwrite.
        await Assert.ThrowsAsync<AbpDbConcurrencyException>(() =>
            _buildingsAppService.UpdateConstraintsAsync(created.Id, new UpdateBuildingConstraintsDto
            {
                Days = created.Days,
                Hours = created.Hours,
                MaxDurationMinutes = 999,
                MaxHorizonDays = created.MaxHorizonDays,
                MinLeadMinutes = created.MinLeadMinutes,
                ConcurrencyStamp = staleStamp,
            }));
    }

    [Fact]
    public async Task Tightening_Hours_Over_An_Existing_Floor_Override_Returns_A_Warning()
    {
        var building = await _buildingsAppService.CreateAsync(NewBuildingInput());

        var floor = new Floor(Guid.NewGuid(), building.Id, "Level 3", 3);
        floor.SetOwnOperatingHours(
            OperatingWindow.Create(TimeOnly.Parse("20:00"), TimeOnly.Parse("23:00")),
            OperatingWindow.FullDay);
        await _floorRepository.InsertAsync(floor);

        var result = await _buildingsAppService.UpdateConstraintsAsync(building.Id, new UpdateBuildingConstraintsDto
        {
            Days = building.Days,
            Hours = new OperatingWindowDto { IsOpen24Hours = false, Open = "06:00", Close = "22:00" },
            MaxDurationMinutes = building.MaxDurationMinutes,
            MaxHorizonDays = building.MaxHorizonDays,
            MinLeadMinutes = building.MinLeadMinutes,
            ConcurrencyStamp = building.ConcurrencyStamp,
        });

        result.Warnings.ShouldContain(w => w.Contains("Level 3"));
    }

    [Fact]
    public async Task Delete_Cascades_To_Floors_And_Spaces_With_A_Shared_Batch_Then_Restore_Brings_Them_Back()
    {
        var building = await _buildingsAppService.CreateAsync(NewBuildingInput());
        var spaceType = await _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "Desk-ish", IconKey.Desk));

        var floor = new Floor(Guid.NewGuid(), building.Id, "Level 1", 1);
        await _floorRepository.InsertAsync(floor);
        var space = new Space(Guid.NewGuid(), floor.Id, "Room A", spaceType.Id, capacity: 4);
        await _spaceRepository.InsertAsync(space);

        await _buildingsAppService.DeleteAsync(building.Id);

        (await _floorRepository.FindAsync(floor.Id)).ShouldBeNull();
        (await _spaceRepository.FindAsync(space.Id)).ShouldBeNull();

        await _buildingsAppService.RestoreAsync(building.Id);

        var restoredBuilding = await _buildingsAppService.GetAsync(building.Id);
        restoredBuilding.ShouldNotBeNull();

        var restoredFloor = await _floorRepository.FindAsync(floor.Id);
        restoredFloor.ShouldNotBeNull();
        restoredFloor!.DeletionBatchId.ShouldBeNull();

        var restoredSpace = await _spaceRepository.FindAsync(space.Id);
        restoredSpace.ShouldNotBeNull();
        restoredSpace!.DeletionBatchId.ShouldBeNull();
    }

    [Fact]
    public async Task Restore_Does_Not_Resurrect_A_Floor_Deleted_Independently_Earlier()
    {
        var building = await _buildingsAppService.CreateAsync(NewBuildingInput());

        var independentlyDeletedFloor = new Floor(Guid.NewGuid(), building.Id, "Already Gone", 9);
        await _floorRepository.InsertAsync(independentlyDeletedFloor);
        await _floorRepository.DeleteAsync(independentlyDeletedFloor);

        var laterFloor = new Floor(Guid.NewGuid(), building.Id, "Still Here Until Delete", 2);
        await _floorRepository.InsertAsync(laterFloor);

        await _buildingsAppService.DeleteAsync(building.Id);
        await _buildingsAppService.RestoreAsync(building.Id);

        var restoredLaterFloor = await _floorRepository.FindAsync(laterFloor.Id);
        restoredLaterFloor.ShouldNotBeNull();

        var stillDeletedFloor = await _floorRepository.FindAsync(independentlyDeletedFloor.Id);
        stillDeletedFloor.ShouldBeNull();
    }

    [Fact]
    public async Task GetListAsync_Pages_Results()
    {
        for (var i = 0; i < 5; i++)
        {
            await _buildingsAppService.CreateAsync(NewBuildingInput($"Building {i:D2}"));
        }

        var page1 = await _buildingsAppService.GetListAsync(new GetBuildingsInput { SkipCount = 0, MaxResultCount = 2 });
        page1.TotalCount.ShouldBeGreaterThanOrEqualTo(5);
        page1.Items.Count.ShouldBe(2);

        var page2 = await _buildingsAppService.GetListAsync(new GetBuildingsInput { SkipCount = 2, MaxResultCount = 2 });
        page2.Items.Count.ShouldBe(2);
        page1.Items.Select(b => b.Id).ShouldNotContain(page2.Items[0].Id);
    }

    [Fact]
    public async Task GetListAsync_Filter_Narrows_By_Name()
    {
        await _buildingsAppService.CreateAsync(NewBuildingInput("Alpha Tower"));
        await _buildingsAppService.CreateAsync(NewBuildingInput("Beta Tower"));

        var result = await _buildingsAppService.GetListAsync(new GetBuildingsInput { Filter = "Alpha" });

        result.Items.ShouldContain(b => b.Name == "Alpha Tower");
        result.Items.ShouldNotContain(b => b.Name == "Beta Tower");
    }

    [Fact]
    public async Task GetListAsync_Without_IncludeDeleted_Excludes_Deleted_Building()
    {
        var building = await _buildingsAppService.CreateAsync(NewBuildingInput("To Be Deleted"));
        await _buildingsAppService.DeleteAsync(building.Id);

        var withoutDeleted = await _buildingsAppService.GetListAsync(new GetBuildingsInput { Filter = "To Be Deleted" });
        withoutDeleted.Items.ShouldNotContain(b => b.Id == building.Id);

        var withDeleted = await _buildingsAppService.GetListAsync(new GetBuildingsInput { Filter = "To Be Deleted", IncludeDeleted = true });
        withDeleted.Items.Single(b => b.Id == building.Id).IsDeleted.ShouldBeTrue();
    }
}
