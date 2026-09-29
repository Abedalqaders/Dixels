using System;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

/// <summary>
/// A space's parents are plain foreign keys (separate aggregates), so the app service has to
/// check they exist and are alive itself — otherwise the database's FK failure comes out as
/// a 500, and a restore can resurrect a space under a floor nobody can see.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class SpacesReferentialIntegrityTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly ISpacesAppService _spacesAppService;
    private readonly IFloorsAppService _floorsAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;

    public SpacesReferentialIntegrityTests()
    {
        _spacesAppService = GetRequiredService<ISpacesAppService>();
        _floorsAppService = GetRequiredService<IFloorsAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _spaceTypeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
    }

    private async Task<Floor> CreateFloorAsync()
    {
        var building = new Building(
            Guid.NewGuid(), "HQ", null, "UTC", OperatingDays.Everyday, OperatingWindow.FullDay,
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0);
        await _buildingRepository.InsertAsync(building);
        return await _floorRepository.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "Level 1", 1));
    }

    private Task<SpaceType> CreateSpaceTypeAsync() =>
        _spaceTypeRepository.InsertAsync(new SpaceType(Guid.NewGuid(), "Type " + Guid.NewGuid().ToString("N")[..6], IconKey.Desk));

    [Fact]
    public async Task Create_Under_An_Unknown_Floor_Is_Not_Found()
    {
        var spaceType = await CreateSpaceTypeAsync();

        await Should.ThrowAsync<EntityNotFoundException>(() => _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = Guid.NewGuid(), Name = "Room A", SpaceTypeId = spaceType.Id, Capacity = 4,
        }));
    }

    [Fact]
    public async Task Create_With_A_Deleted_SpaceType_Is_Not_Found()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        await _spaceTypeRepository.DeleteAsync(spaceType);

        await Should.ThrowAsync<EntityNotFoundException>(() => _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id, Name = "Room A", SpaceTypeId = spaceType.Id, Capacity = 4,
        }));
    }

    [Fact]
    public async Task Update_To_An_Unknown_SpaceType_Is_Not_Found()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        var created = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id, Name = "Room A", SpaceTypeId = spaceType.Id, Capacity = 4,
        });

        await Should.ThrowAsync<EntityNotFoundException>(() => _spacesAppService.UpdateAsync(created.Id, new UpdateSpaceDto
        {
            Name = "Room A", SpaceTypeId = Guid.NewGuid(), Capacity = 4,
        }));
    }

    [Fact]
    public async Task Restore_Under_A_Deleted_Floor_Is_Refused()
    {
        var floor = await CreateFloorAsync();
        var spaceType = await CreateSpaceTypeAsync();
        var space = await _spacesAppService.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id, Name = "Room A", SpaceTypeId = spaceType.Id, Capacity = 4,
        });

        await _floorsAppService.DeleteAsync(floor.Id); // cascades to the space

        var ex = await Should.ThrowAsync<BusinessException>(() => _spacesAppService.RestoreAsync(space.Id));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.ParentIsDeleted);
        ex.Data["parent"].ShouldBe("floor");
    }
}
