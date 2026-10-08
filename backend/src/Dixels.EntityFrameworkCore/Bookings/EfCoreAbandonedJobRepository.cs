using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.Bookings;

public class EfCoreAbandonedJobRepository : IAbandonedJobRepository, ITransientDependency
{
    private readonly IDbContextProvider<DixelsDbContext> _dbContextProvider;

    public EfCoreAbandonedJobRepository(IDbContextProvider<DixelsDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task<int> DeleteAbandonedAsync(
        IReadOnlyCollection<string> jobNames,
        DateTime createdBefore,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();

        // No index fits (ABP's starts with ApplicationName) and none is needed: sent jobs are
        // deleted at once, so the table only holds the few waiting to be sent and the abandoned
        // ones, which this cleanup itself keeps to the retention window.
        return await dbContext.Set<BackgroundJobRecord>()
            .Where(j => j.IsAbandoned && jobNames.Contains(j.JobName) && j.CreationTime < createdBefore)
            .OrderBy(j => j.CreationTime)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
