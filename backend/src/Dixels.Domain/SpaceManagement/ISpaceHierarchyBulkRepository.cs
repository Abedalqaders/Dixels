using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dixels.SpaceManagement;

/// <summary>
/// Soft-deletes a building or a floor with everything under it, one UPDATE per table instead
/// of loading and saving every room. Each row gets what a repository soft-delete gives it
/// (deleted, when, by whom, a new concurrency stamp) plus the shared delete batch id, so a
/// restore brings back exactly what was deleted together (see <see cref="SpaceHierarchyManager"/>).
/// Rows already deleted are left as they are, with their own batch.
///
/// Bulk updates skip EF's change tracker, so no per-entity events are raised: the caller
/// announces the delete itself, with the rooms it took.
/// </summary>
public interface ISpaceHierarchyBulkRepository
{
    /// <summary>The building, its floors and their rooms. Returns the rooms' ids.</summary>
    Task<List<Guid>> SoftDeleteBuildingAsync(Guid buildingId, Guid batchId, DateTime deletionTime, Guid? deleterId, CancellationToken cancellationToken = default);

    /// <summary>The floor and its rooms. Returns the rooms' ids.</summary>
    Task<List<Guid>> SoftDeleteFloorAsync(Guid floorId, Guid batchId, DateTime deletionTime, Guid? deleterId, CancellationToken cancellationToken = default);
}
