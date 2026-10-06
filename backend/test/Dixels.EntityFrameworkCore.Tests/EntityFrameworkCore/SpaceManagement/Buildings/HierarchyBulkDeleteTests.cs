using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

/// <summary>
/// The bulk delete (one UPDATE per table) leaves every row exactly as the per-row delete it
/// replaced did: the same rows deleted, with the same batch id, deleter, times and a renewed
/// stamp, and a floor deleted earlier left with its own state.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class HierarchyBulkDeleteTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private static readonly Guid Admin = Guid.NewGuid();

    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;

    public HierarchyBulkDeleteTests()
    {
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
    }

    private sealed record Row(
        string Node,
        bool IsDeleted,
        string Batch,
        Guid? DeleterId,
        bool HasDeletionTime,
        Guid? LastModifierId,
        bool HasModificationTime,
        bool StampRenewed);

    /// <summary>
    /// A building with two floors of two rooms, and a third floor (with a room) deleted on
    /// its own earlier. Returns every node in a fixed order, by name, for comparing two copies.
    /// </summary>
    private Task<List<(string Node, Guid Id)>> CreateHierarchyAsync() => WithUnitOfWorkAsync(async () =>
    {
        var nodes = new List<(string, Guid)>();
        var building = await _buildingRepository.InsertAsync(new Building(
            Guid.NewGuid(), "en", "Bulk " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), OperatingWindow.FullDay,
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0));
        nodes.Add(("building", building.Id));
        var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();

        for (var f = 1; f <= 3; f++)
        {
            var floor = await _floorRepository.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", $"Level {f}", f));
            nodes.Add(($"floor {f}", floor.Id));
            for (var r = 1; r <= (f == 3 ? 1 : 2); r++)
            {
                var space = await _spaceRepository.InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", $"Room {f}.{r}", spaceType.Id, 4));
                nodes.Add(($"room {f}.{r}", space.Id));
            }
        }

        return nodes;
    });

    private async Task DeleteFloorThreeEarlierAsync(List<(string Node, Guid Id)> nodes)
    {
        using var _ = ActAs(Admin);
        await WithUnitOfWorkAsync(async () =>
        {
            await _spaceRepository.DeleteAsync(nodes.Single(n => n.Node == "room 3.1").Id);
            await _floorRepository.DeleteAsync(nodes.Single(n => n.Node == "floor 3").Id);
        });
    }

    private async Task<Dictionary<Guid, string>> StampsAsync(List<(string Node, Guid Id)> nodes)
    {
        using (GetRequiredService<IDataFilter>().Disable<ISoftDelete>())
        {
            return await WithUnitOfWorkAsync(async () =>
            {
                var stamps = new Dictionary<Guid, string>();
                foreach (var (node, id) in nodes)
                {
                    stamps[id] = (await LoadAsync(node, id)).ConcurrencyStamp;
                }

                return stamps;
            });
        }
    }

    private async Task<List<Row>> SnapshotAsync(List<(string Node, Guid Id)> nodes, Guid batchId, Dictionary<Guid, string> stampsBefore)
    {
        using (GetRequiredService<IDataFilter>().Disable<ISoftDelete>())
        {
            return await WithUnitOfWorkAsync(async () =>
            {
                var rows = new List<Row>();
                foreach (var (node, id) in nodes)
                {
                    var entity = await LoadAsync(node, id);
                    var batch = BatchOf(entity) switch
                    {
                        null => "none",
                        var b when b == batchId => "this delete",
                        _ => "another",
                    };
                    rows.Add(new Row(
                        node, entity.IsDeleted, batch, entity.DeleterId, entity.DeletionTime is not null,
                        entity.LastModifierId, entity.LastModificationTime is not null, entity.ConcurrencyStamp != stampsBefore[id]));
                }

                return rows;
            });
        }
    }

    private async Task<FullAuditedAggregateRoot<Guid>> LoadAsync(string node, Guid id) => node switch
    {
        "building" => await _buildingRepository.GetAsync(id),
        _ when node.StartsWith("floor") => await _floorRepository.GetAsync(id),
        _ => await _spaceRepository.GetAsync(id),
    };

    private static Guid? BatchOf(FullAuditedAggregateRoot<Guid> entity) => entity switch
    {
        Building b => b.DeletionBatchId,
        Floor f => f.DeletionBatchId,
        Space s => s.DeletionBatchId,
        _ => throw new ArgumentOutOfRangeException(nameof(entity)),
    };

    private IDisposable ActAs(Guid userId) =>
        GetRequiredService<ICurrentPrincipalAccessor>().Change(
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    /// <summary>The per-row delete the app service used before: stamp and save, then delete each, children first.</summary>
    private async Task DeleteTheOldWayAsync(Guid buildingId, Guid batchId)
    {
        using var _ = ActAs(Admin);
        await WithUnitOfWorkAsync(async () =>
        {
            var building = await _buildingRepository.GetAsync(buildingId);
            var floors = await _floorRepository.GetListAsync(f => f.BuildingId == buildingId);
            var floorIds = floors.Select(f => f.Id).ToList();
            var spaces = await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId));

            GetRequiredService<SpaceHierarchyManager>().MarkForSoftDelete(batchId, building, floors, spaces);
            await _buildingRepository.UpdateAsync(building);
            await _floorRepository.UpdateManyAsync(floors);
            await _spaceRepository.UpdateManyAsync(spaces);
            await GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();

            await _spaceRepository.DeleteManyAsync(spaces);
            await _floorRepository.DeleteManyAsync(floors);
            await _buildingRepository.DeleteAsync(building);
        });
    }

    [Fact]
    public async Task The_bulk_delete_changes_the_same_rows_the_same_way_as_the_per_row_delete()
    {
        var oldWay = await CreateHierarchyAsync();
        var bulk = await CreateHierarchyAsync();
        await DeleteFloorThreeEarlierAsync(oldWay);
        await DeleteFloorThreeEarlierAsync(bulk);
        var oldStamps = await StampsAsync(oldWay);
        var bulkStamps = await StampsAsync(bulk);
        var oldBatch = Guid.NewGuid();
        var bulkBatch = Guid.NewGuid();

        await DeleteTheOldWayAsync(oldWay[0].Id, oldBatch);
        await WithUnitOfWorkAsync(() => GetRequiredService<ISpaceHierarchyBulkRepository>()
            .SoftDeleteBuildingAsync(bulk[0].Id, bulkBatch, DateTime.Now, Admin));

        var expected = await SnapshotAsync(oldWay, oldBatch, oldStamps);
        var actual = await SnapshotAsync(bulk, bulkBatch, bulkStamps);
        actual.ShouldBe(expected);

        // And what that is, spelled out: the building, both live floors and their rooms are
        // this delete's; floor 3 and its room keep the delete they had.
        expected.Where(r => r.Batch == "this delete").Select(r => r.Node).ShouldBe(new[]
        {
            "building", "floor 1", "room 1.1", "room 1.2", "floor 2", "room 2.1", "room 2.2",
        });
        expected.ShouldAllBe(r => r.IsDeleted && r.DeleterId == Admin && r.HasDeletionTime);
        expected.Where(r => r.Node.EndsWith('3') || r.Node.EndsWith("3.1")).ShouldAllBe(r => r.Batch == "none" && !r.StampRenewed);
    }

    [Fact]
    public async Task The_bulk_floor_delete_returns_its_rooms_and_leaves_the_building()
    {
        var nodes = await CreateHierarchyAsync();
        var floor = nodes.Single(n => n.Node == "floor 1").Id;
        var batch = Guid.NewGuid();

        var spaceIds = await WithUnitOfWorkAsync(() => GetRequiredService<ISpaceHierarchyBulkRepository>()
            .SoftDeleteFloorAsync(floor, batch, DateTime.Now, Admin));

        spaceIds.ShouldBe(nodes.Where(n => n.Node.StartsWith("room 1.")).Select(n => n.Id), ignoreOrder: true);
        var rows = await SnapshotAsync(nodes, batch, await StampsAsync(nodes));
        rows.Where(r => r.Batch == "this delete").Select(r => r.Node).ShouldBe(new[] { "floor 1", "room 1.1", "room 1.2" });
        rows.Single(r => r.Node == "building").IsDeleted.ShouldBeFalse();
    }
}
