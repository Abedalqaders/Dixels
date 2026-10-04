using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace Dixels.Bookings;

/// <summary>
/// Once a minute, sends the booking reminders that are due (see <see cref="BookingReminders"/>).
/// Started by the API host (DixelsWebModule), not the migrator.
/// </summary>
public class BookingReminderWorker : AsyncPeriodicBackgroundWorkerBase
{
    public BookingReminderWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 60_000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await workerContext.ServiceProvider.GetRequiredService<BookingReminders>().SendDueAsync();
    }
}
