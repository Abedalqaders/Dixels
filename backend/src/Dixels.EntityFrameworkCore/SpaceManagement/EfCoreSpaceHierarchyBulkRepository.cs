using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.SpaceManagement;

public class EfCoreSpaceHierarchyBulkRepository : ISpaceHierarchyBulkRepository, ITransientDependency
{
    private readonly IDbContextProvider<DixelsDbContext> _dbContextProvider;

    public EfCoreSpaceHierarchyBulkRepository(IDbContextProvider<DixelsDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task<List<Guid>> SoftDeleteBuildingAsync(
        Guid buildingId,
        Guid batchId,
        DateTime deletionTime,
        Guid? deleterId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        // The soft-delete filter applies to these queries and updates as to any other: only
        // what's still there is read and stamped.
        var floorIds = await dbContext.Floors.Where(f => f.BuildingId == buildingId).Select(f => f.Id).ToListAsync(cancellationToken);
        var spaceIds = await dbContext.Spaces.Where(s => floorIds.Contains(s.FloorId)).Select(s => s.Id).ToListAsync(cancellationToken);
        var stamp = NewStamp();

        // Children first, like a database cascade (and like the per-row delete this replaces).
        await dbContext.Spaces.Where(s => spaceIds.Contains(s.Id)).ExecuteUpdateAsync(u => u
            .SetProperty(s => s.IsDeleted, true)
            .SetProperty(s => s.DeletionTime, deletionTime)
            .SetProperty(s => s.DeleterId, deleterId)
            .SetProperty(s => s.DeletionBatchId, batchId)
            .SetProperty(s => s.LastModificationTime, deletionTime)
            .SetProperty(s => s.LastModifierId, deleterId)
            .SetProperty(s => s.ConcurrencyStamp, stamp), cancellationToken);

        await dbContext.Floors.Where(f => floorIds.Contains(f.Id)).ExecuteUpdateAsync(u => u
            .SetProperty(f => f.IsDeleted, true)
            .SetProperty(f => f.DeletionTime, deletionTime)
            .SetProperty(f => f.DeleterId, deleterId)
            .SetProperty(f => f.DeletionBatchId, batchId)
            .SetProperty(f => f.LastModificationTime, deletionTime)
            .SetProperty(f => f.LastModifierId, deleterId)
            .SetProperty(f => f.ConcurrencyStamp, stamp), cancellationToken);

        await dbContext.Buildings.Where(b => b.Id == buildingId).ExecuteUpdateAsync(u => u
            .SetProperty(b => b.IsDeleted, true)
            .SetProperty(b => b.DeletionTime, deletionTime)
            .SetProperty(b => b.DeleterId, deleterId)
            .SetProperty(b => b.DeletionBatchId, batchId)
            .SetProperty(b => b.LastModificationTime, deletionTime)
            .SetProperty(b => b.LastModifierId, deleterId)
            .SetProperty(b => b.ConcurrencyStamp, stamp), cancellationToken);

        return spaceIds;
    }

    public async Task<List<Guid>> SoftDeleteFloorAsync(
        Guid floorId,
        Guid batchId,
        DateTime deletionTime,
        Guid? deleterId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        var spaceIds = await dbContext.Spaces.Where(s => s.FloorId == floorId).Select(s => s.Id).ToListAsync(cancellationToken);
        var stamp = NewStamp();

        await dbContext.Spaces.Where(s => spaceIds.Contains(s.Id)).ExecuteUpdateAsync(u => u
            .SetProperty(s => s.IsDeleted, true)
            .SetProperty(s => s.DeletionTime, deletionTime)
            .SetProperty(s => s.DeleterId, deleterId)
            .SetProperty(s => s.DeletionBatchId, batchId)
            .SetProperty(s => s.LastModificationTime, deletionTime)
            .SetProperty(s => s.LastModifierId, deleterId)
            .SetProperty(s => s.ConcurrencyStamp, stamp), cancellationToken);

        await dbContext.Floors.Where(f => f.Id == floorId).ExecuteUpdateAsync(u => u
            .SetProperty(f => f.IsDeleted, true)
            .SetProperty(f => f.DeletionTime, deletionTime)
            .SetProperty(f => f.DeleterId, deleterId)
            .SetProperty(f => f.DeletionBatchId, batchId)
            .SetProperty(f => f.LastModificationTime, deletionTime)
            .SetProperty(f => f.LastModifierId, deleterId)
            .SetProperty(f => f.ConcurrencyStamp, stamp), cancellationToken);

        return spaceIds;
    }

    // A bulk update doesn't go through ABP's save hooks, so the stamp a save would renew is
    // renewed here: an edit still open on a deleted row can't save over it.
    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
