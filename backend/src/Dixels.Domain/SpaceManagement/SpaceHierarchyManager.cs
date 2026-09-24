using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Services;

namespace Dixels.SpaceManagement;

/// <summary>
/// Orchestrates cascading soft-delete/restore across the Building → Floor → Space
/// hierarchy — ABP does not auto-cascade soft-deletes, so this fills that gap explicitly.
///
/// Deliberately DB-free: it only stamps/reads <c>DeletionBatchId</c> on already-loaded
/// entities and returns which ones to act on. The caller (a future application service) is
/// responsible for loading the relevant entities via repositories and persisting the result
/// (calling <c>DeleteAsync</c>/setting <c>IsDeleted = false</c> as appropriate) — that keeps
/// this class trivially unit-testable with plain in-memory instances.
///
/// A fresh batch id is generated once per top-level delete operation by the caller and
/// shared by every entity cascaded from it, so a later restore can be scoped to exactly what
/// was deleted together — restoring a Building must not resurrect a Floor that was deleted
/// independently and earlier.
/// </summary>
public class SpaceHierarchyManager : IDomainService
{
    public void MarkForSoftDelete(Guid batchId, Building building, IReadOnlyList<Floor> floors, IReadOnlyList<Space> spaces)
    {
        building.DeletionBatchId = batchId;

        foreach (var floor in floors)
        {
            floor.DeletionBatchId = batchId;
        }

        foreach (var space in spaces)
        {
            space.DeletionBatchId = batchId;
        }
    }

    public void MarkForSoftDelete(Guid batchId, Floor floor, IReadOnlyList<Space> spaces)
    {
        floor.DeletionBatchId = batchId;

        foreach (var space in spaces)
        {
            space.DeletionBatchId = batchId;
        }
    }

    /// <summary>
    /// Of the floors soft-deleted under a building (loaded with the soft-delete filter
    /// disabled), returns and clears the batch id on only the ones that share the batch id
    /// being restored.
    /// </summary>
    public IReadOnlyList<Floor> SelectAndClearForRestore(Guid batchId, IReadOnlyList<Floor> candidateFloors)
    {
        var matching = candidateFloors.Where(f => f.DeletionBatchId == batchId).ToList();

        foreach (var floor in matching)
        {
            floor.DeletionBatchId = null;
        }

        return matching;
    }

    public IReadOnlyList<Space> SelectAndClearForRestore(Guid batchId, IReadOnlyList<Space> candidateSpaces)
    {
        var matching = candidateSpaces.Where(s => s.DeletionBatchId == batchId).ToList();

        foreach (var space in matching)
        {
            space.DeletionBatchId = null;
        }

        return matching;
    }
}
