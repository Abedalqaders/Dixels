using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace Dixels.Bookings;

/// <summary>
/// Once an hour, deletes outside guests' details past the retention window (see
/// <see cref="ExternalGuestCleanup"/>). Started by the API host (DixelsWebModule), not the migrator.
/// </summary>
public class ExternalGuestCleanupWorker : AsyncPeriodicBackgroundWorkerBase
{
    public ExternalGuestCleanupWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 60 * 60 * 1000;
        // Also once at start: the timer's first tick is an hour away, so a host restarted more
        // often than that (a deploy, dotnet watch) would otherwise never clean.
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await workerContext.ServiceProvider.GetRequiredService<ExternalGuestCleanup>().RunAsync();
    }
}
