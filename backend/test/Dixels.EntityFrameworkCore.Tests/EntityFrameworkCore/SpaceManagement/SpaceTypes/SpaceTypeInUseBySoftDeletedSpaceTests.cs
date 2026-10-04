using System;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class SpaceTypeInUseBySoftDeletedSpaceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly ISpaceTypesAppService _spaceTypesAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;

    public SpaceTypeInUseBySoftDeletedSpaceTests()
    {
        _spaceTypesAppService = GetRequiredService<ISpaceTypesAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
    }

    /// <summary>
    /// A soft-deleted space can be restored, and a restored space whose type no longer exists
    /// breaks every lookup of it — so a type is "in use" by deleted spaces too.
    /// </summary>
    [Fact]
    public async Task Delete_When_Only_A_SoftDeleted_Space_Uses_It_Still_Throws()
    {
        var spaceType = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Names = [new() { Language = "en", Name = "Booth " + Guid.NewGuid().ToString("N")[..6] }] });
        var building = await _buildingRepository.InsertAsync(new Building(
            Guid.NewGuid(), "en", "HQ", null, "UTC", OperatingDays.Everyday, OperatingWindow.FullDay,
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
        var floor = await _floorRepository.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
        var space = await _spaceRepository.InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Booth 1", spaceType.Id, capacity: 1));

        await _spaceRepository.DeleteAsync(space);

        var ex = await Should.ThrowAsync<BusinessException>(() => _spaceTypesAppService.DeleteAsync(spaceType.Id));
        ex.Code.ShouldBe(DixelsDomainErrorCodes.SpaceTypeInUse);
    }
}
