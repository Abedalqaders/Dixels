using System;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;
using static Dixels.TestNames;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

/// <summary>See <see cref="SpacesReferentialIntegrityTests"/>: the same two rules one level up.</summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class FloorsReferentialIntegrityTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IFloorsAppService _floorsAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;

    public FloorsReferentialIntegrityTests()
    {
        _floorsAppService = GetRequiredService<IFloorsAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
    }

    private Task<Building> CreateBuildingAsync() => _buildingRepository.InsertAsync(new Building(
        Guid.NewGuid(), "en", "HQ", null, "UTC", OperatingDays.Everyday, OperatingWindow.FullDay,
        maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));

    [Fact]
    public async Task Create_Under_An_Unknown_Building_Is_Not_Found()
    {
        await Should.ThrowAsync<EntityNotFoundException>(() => _floorsAppService.CreateAsync(new CreateFloorDto
        {
            BuildingId = Guid.NewGuid(), Names = En("Level 1"), FloorNumber = 1,
        }));
    }

    [Fact]
    public async Task Restore_Under_A_Deleted_Building_Is_Refused()
    {
        var building = await CreateBuildingAsync();
        var floor = await _floorRepository.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        await _floorRepository.DeleteAsync(floor);
        await _buildingRepository.DeleteAsync(building);

        var ex = await Should.ThrowAsync<BusinessException>(() => _floorsAppService.RestoreAsync(floor.Id));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.ParentIsDeleted);
        ex.Data["parent"].ShouldBe("building");
    }
}
