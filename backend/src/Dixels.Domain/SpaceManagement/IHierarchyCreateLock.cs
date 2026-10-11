using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dixels.SpaceManagement;

/// <summary>
/// Holds one admin's key until the current transaction ends, so two copies of the same "add"
/// sent at once run one after the other: the second then finds what the first saved.
/// </summary>
public interface IHierarchyCreateLock
{
    Task LockAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);
}
