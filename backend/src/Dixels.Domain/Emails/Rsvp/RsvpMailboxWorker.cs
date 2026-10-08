using System;
using System.Threading;
using System.Threading.Tasks;
using Dixels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Settings;

namespace Dixels.Emails.Rsvp;

/// <summary>
/// Reads guests' answers from the rsvp@ mailbox while <see cref="DixelsSettings.RsvpMailboxEnabled"/>
/// is on: handles what's waiting, then waits on the open connection (IMAP IDLE, else a check
/// every minute), so an answer applies within seconds. Not a timer: IDLE needs the connection
/// held. One host reads at a time (the lock); the others try again every minute. After a
/// failure (the mail server or the database away) it reconnects, waiting longer each time.
/// Started by the API host (DixelsWebModule), not the migrator.
/// </summary>
public class RsvpMailboxWorker : BackgroundWorkerBase
{
    public const string LockName = "Dixels:RsvpMailbox";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private CancellationTokenSource? _stopping;
    private Task? _running;

    public RsvpMailboxWorker(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);
        _stopping = new CancellationTokenSource();
        _running = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
    }

    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_stopping is not null && _running is not null)
        {
            await _stopping.CancelAsync();
            try
            {
                await _running;
            }
            catch (OperationCanceledException)
            {
                // Stopping, as asked.
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task RunAsync(CancellationToken stopping)
    {
        var failures = 0;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await ReadWhileEnabledAsync(stopping);
                failures = 0;
                await Task.Delay(PollInterval, stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                failures++;
                var wait = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, PollInterval.Ticks * (1L << Math.Min(failures - 1, 4))));
                Logger.LogWarning(ex, "Reading the rsvp mailbox failed ({Failures} in a row); trying again in {Wait}.", failures, wait);
                await Task.Delay(wait, stopping).ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
    }

    /// <summary>One connection: returns when the setting is off or another host holds the lock.</summary>
    private async Task ReadWhileEnabledAsync(CancellationToken stopping)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingProvider>();
        if (!await settings.IsTrueAsync(DixelsSettings.RsvpMailboxEnabled))
        {
            return;
        }

        await using var handle = await scope.ServiceProvider.GetRequiredService<IAbpDistributedLock>()
            .TryAcquireAsync(LockName, cancellationToken: stopping);
        if (handle is null)
        {
            return;
        }

        await using var mailbox = scope.ServiceProvider.GetRequiredService<IRsvpMailbox>();
        var reader = scope.ServiceProvider.GetRequiredService<RsvpMailboxReader>();
        while (await settings.IsTrueAsync(DixelsSettings.RsvpMailboxEnabled))
        {
            await reader.HandleWaitingAsync(mailbox, stopping);
            await mailbox.WaitForNewAsync(PollInterval, stopping);
        }
    }
}
