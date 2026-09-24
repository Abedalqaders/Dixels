using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class SpaceTypesAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly ISpaceTypesAppService _spaceTypesAppService;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;

    public SpaceTypesAppServiceTests()
    {
        _spaceTypesAppService = GetRequiredService<ISpaceTypesAppService>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
    }

    [Fact]
    public async Task Create_Then_GetList_Returns_The_New_SpaceType()
    {
        var created = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto
        {
            Name = "Phone Booth",
            IconKey = IconKey.FocusPod
        });

        var list = await _spaceTypesAppService.GetListAsync();

        list.Items.ShouldContain(t => t.Id == created.Id && t.Name == "Phone Booth" && t.IconKey == IconKey.FocusPod);
    }

    [Fact]
    public async Task Create_With_Duplicate_Name_Throws()
    {
        await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "Duplicate Me" });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "Duplicate Me" }));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.SpaceTypeNameAlreadyExists);
    }

    [Fact]
    public async Task Update_Renames_And_Changes_Icon()
    {
        var created = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto
        {
            Name = "Old Name",
            IconKey = IconKey.Desk
        });

        var updated = await _spaceTypesAppService.UpdateAsync(created.Id, new UpdateSpaceTypeDto
        {
            Name = "New Name",
            IconKey = IconKey.MeetingRoom
        });

        updated.Name.ShouldBe("New Name");
        updated.IconKey.ShouldBe(IconKey.MeetingRoom);
    }

    [Fact]
    public async Task Update_To_Another_SpaceTypes_Name_Throws()
    {
        await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "Taken" });
        var other = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "Free" });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _spaceTypesAppService.UpdateAsync(other.Id, new UpdateSpaceTypeDto { Name = "Taken" }));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.SpaceTypeNameAlreadyExists);
    }

    [Fact]
    public async Task Update_Keeping_Its_Own_Name_Does_Not_Throw()
    {
        var created = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "Stays The Same" });

        var updated = await _spaceTypesAppService.UpdateAsync(created.Id, new UpdateSpaceTypeDto
        {
            Name = "Stays The Same",
            IconKey = IconKey.Generic
        });

        updated.IconKey.ShouldBe(IconKey.Generic);
    }

    [Fact]
    public async Task Delete_Not_In_Use_Succeeds()
    {
        var created = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "Unused Type" });

        await _spaceTypesAppService.DeleteAsync(created.Id);

        var list = await _spaceTypesAppService.GetListAsync();
        list.Items.ShouldNotContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task Delete_When_In_Use_By_A_Space_Throws()
    {
        var spaceType = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "In Use Type" });
        await CreateSpaceUsingSpaceTypeAsync(spaceType.Id);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _spaceTypesAppService.DeleteAsync(spaceType.Id));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.SpaceTypeInUse);
    }

    [Fact]
    public async Task Deleted_SpaceTypes_Name_Can_Be_Reused()
    {
        var first = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "Reusable Name" });
        await _spaceTypesAppService.DeleteAsync(first.Id);

        // Proves the partial unique index (WHERE "IsDeleted" = false) through the actual
        // application service, not just a raw SQL check against the table.
        var second = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Name = "Reusable Name" });

        second.Id.ShouldNotBe(first.Id);
    }

    private async Task CreateSpaceUsingSpaceTypeAsync(Guid spaceTypeId)
    {
        var building = new Building(
            Guid.NewGuid(),
            "Test Building",
            null,
            "UTC",
            OperatingDays.Everyday,
            OperatingWindow.FullDay,
            maxDurationMinutes: 60,
            maxHorizonDays: 30,
            minLeadMinutes: 0);
        await _buildingRepository.InsertAsync(building);

        var floor = new Floor(Guid.NewGuid(), building.Id, "Test Floor", null);
        await _floorRepository.InsertAsync(floor);

        var space = new Space(Guid.NewGuid(), floor.Id, "Test Space", spaceTypeId, capacity: 4);
        await _spaceRepository.InsertAsync(space);
    }
}
