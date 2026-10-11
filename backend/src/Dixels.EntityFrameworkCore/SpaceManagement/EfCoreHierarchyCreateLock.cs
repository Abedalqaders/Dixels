using System;
using System.Threading;
using System.Threading.Tasks;
using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.SpaceManagement;

public class EfCoreHierarchyCreateLock : IHierarchyCreateLock, ITransientDependency
{
    // A Postgres advisory lock held until the transaction ends (commit or rollback): no table
    // row is locked, and two different keys almost never wait for each other (only if their
    // 64-bit hashes collide, which just makes one wait a moment).
    private const string LockSql = "SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))";

    private readonly IDbContextProvider<DixelsDbContext> _dbContextProvider;

    public EfCoreHierarchyCreateLock(IDbContextProvider<DixelsDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task LockAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();

        // SQLite (the unit-test database) has no such locks — it serialises writers on its own.
        // The unique (creator, key) index is the backstop either way.
        if (dbContext.Database.IsNpgsql())
        {
            await dbContext.Database.ExecuteSqlRawAsync(LockSql, new object[] { $"hierarchy-create:{userId}:{idempotencyKey}" }, cancellationToken);
        }
    }
}
