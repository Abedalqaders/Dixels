using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.SpaceManagement;

/// <summary>
/// Makes an admin's "add building / floor / space" safe to send twice (a double-click, a
/// network blip, a retry after a lost answer): the add dialog sends one key per opening, and a
/// second request with that key gets the first one's result instead of a second copy. The same
/// idea as a booking's key (BookingManager.CreateAsync).
/// </summary>
public class HierarchyCreateReplays : DomainService
{
    private readonly IHierarchyCreateLock _lock;
    private readonly IDataFilter _dataFilter;

    public HierarchyCreateReplays(IHierarchyCreateLock @lock, IDataFilter dataFilter)
    {
        _lock = @lock;
        _dataFilter = dataFilter;
    }

    /// <summary>
    /// What this admin already made with this key, or null when it's new (save it, with
    /// <c>SetIdempotencyKey</c>). Must run inside the create's transaction: the key stays locked
    /// until it commits, so a copy of the request sent at the same moment waits here and then
    /// finds this one's result.
    ///
    /// A key that was used for something else (<paramref name="isSameRequest"/> says no), or
    /// whose result has since been deleted, is <see cref="DixelsDomainErrorCodes.HierarchyIdempotencyKeyReused"/>:
    /// handing back a different or deleted item as "created" would be wrong.
    /// </summary>
    public async Task<T?> FindAsync<T>(
        IRepository<T, Guid> repository,
        Guid userId,
        string idempotencyKey,
        Func<T, bool> isSameRequest)
        where T : class, IEntity<Guid>, IMayHaveCreator, ISoftDelete, IHasIdempotencyKey
    {
        await _lock.LockAsync(userId, idempotencyKey);

        T? existing;
        // Deleted ones too: a retry must not quietly make a new copy of something since removed.
        using (_dataFilter.Disable<ISoftDelete>())
        {
            existing = await repository.FindAsync(e => e.CreatorId == userId && e.IdempotencyKey == idempotencyKey, includeDetails: true);
        }

        if (existing is not null && (existing.IsDeleted || !isSameRequest(existing)))
        {
            throw new BusinessException(DixelsDomainErrorCodes.HierarchyIdempotencyKeyReused);
        }

        return existing;
    }
}
