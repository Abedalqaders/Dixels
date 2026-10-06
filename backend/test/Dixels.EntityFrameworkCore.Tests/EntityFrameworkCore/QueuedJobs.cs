using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace Dixels.EntityFrameworkCore;

/// <summary>
/// The test module turns ABP's job worker off (DixelsTestBaseModule), so queued jobs wait in
/// the store. This runs them the way the worker would: what's waiting, each once, then removed.
/// </summary>
public static class QueuedJobs
{
    public static async Task<List<BackgroundJobRecord>> WaitingAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var jobs = (await scope.ServiceProvider.GetRequiredService<IRepository<BackgroundJobRecord, Guid>>()
                .GetListAsync(j => !j.IsAbandoned))
            .OrderBy(j => j.CreationTime)
            .ToList();
        await uow.CompleteAsync();
        return jobs;
    }

    public static async Task RunAllAsync(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<AbpBackgroundJobOptions>>().Value;
        var serializer = services.GetRequiredService<IBackgroundJobSerializer>();
        var executer = services.GetRequiredService<IBackgroundJobExecuter>();

        foreach (var job in await WaitingAsync(services))
        {
            var configuration = options.GetJob(job.JobName);
            var args = serializer.Deserialize(job.JobArgs, configuration.ArgsType);

            using var scope = services.CreateScope();
            await executer.ExecuteAsync(new JobExecutionContext(scope.ServiceProvider, configuration.JobType, args));

            using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            await scope.ServiceProvider.GetRequiredService<IRepository<BackgroundJobRecord, Guid>>().DeleteAsync(job.Id);
            await uow.CompleteAsync();
        }
    }
}
