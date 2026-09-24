using System;
using Shouldly;
using Xunit;

namespace Dixels.SpaceManagement.Tests;

public class SpaceHierarchyManagerTests
{
    private readonly SpaceHierarchyManager _manager = new();

    private static Building CreateBuilding()
        => new(
            Guid.NewGuid(), "Ridge House", "RH-01", "Asia/Amman",
            ValueObjects.OperatingDays.Everyday, ValueObjects.OperatingWindow.FullDay,
            480, 14, 15);

    private static Floor CreateFloor(Guid buildingId, string name = "Level 1")
        => new(Guid.NewGuid(), buildingId, name, floorNumber: 1);

    private static Space CreateSpace(Guid floorId, string name = "Desk 1")
        => new(Guid.NewGuid(), floorId, name, Guid.NewGuid(), capacity: 1);

    [Fact]
    public void MarkForSoftDelete_stamps_the_same_batch_id_on_every_descendant()
    {
        var building = CreateBuilding();
        var floor = CreateFloor(building.Id);
        var space = CreateSpace(floor.Id);
        var batchId = Guid.NewGuid();

        _manager.MarkForSoftDelete(batchId, building, [floor], [space]);

        building.DeletionBatchId.ShouldBe(batchId);
        floor.DeletionBatchId.ShouldBe(batchId);
        space.DeletionBatchId.ShouldBe(batchId);
    }

    [Fact]
    public void Restore_only_resurrects_floors_from_the_same_batch()
    {
        var building = CreateBuilding();
        var batchId = Guid.NewGuid();

        var floorFromThisBatch = CreateFloor(building.Id, "Level 1");
        floorFromThisBatch.DeletionBatchId = batchId;

        var floorDeletedIndependentlyEarlier = CreateFloor(building.Id, "Level 2");
        floorDeletedIndependentlyEarlier.DeletionBatchId = Guid.NewGuid();

        var restored = _manager.SelectAndClearForRestore(
            batchId,
            [floorFromThisBatch, floorDeletedIndependentlyEarlier]);

        restored.ShouldHaveSingleItem();
        restored[0].ShouldBe(floorFromThisBatch);
        floorFromThisBatch.DeletionBatchId.ShouldBeNull();
        floorDeletedIndependentlyEarlier.DeletionBatchId.ShouldNotBeNull();
    }

    [Fact]
    public void Restore_only_resurrects_spaces_from_the_same_batch()
    {
        var floor = CreateFloor(Guid.NewGuid());
        var batchId = Guid.NewGuid();

        var spaceFromThisBatch = CreateSpace(floor.Id, "Desk 1");
        spaceFromThisBatch.DeletionBatchId = batchId;

        var spaceDeletedIndependentlyEarlier = CreateSpace(floor.Id, "Desk 2");
        spaceDeletedIndependentlyEarlier.DeletionBatchId = Guid.NewGuid();

        var restored = _manager.SelectAndClearForRestore(
            batchId,
            [spaceFromThisBatch, spaceDeletedIndependentlyEarlier]);

        restored.ShouldHaveSingleItem();
        restored[0].ShouldBe(spaceFromThisBatch);
    }
}
