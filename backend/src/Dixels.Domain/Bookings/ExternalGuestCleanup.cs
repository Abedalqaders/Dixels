using System;
using System.Threading.Tasks;
using Dixels.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace Dixels.Bookings;

/// <summary>
/// Outside guests' details (email, name) are personal data of people with no account, so they're
/// deleted <see cref="DixelsSettings.ExternalGuestRetentionDays"/> days after the booking ends or
/// is cancelled. A series' own list goes once every one of its dates is past that. The head count
/// stays, colleagues' rows stay, and nothing is announced. <see cref="ExternalGuestCleanupWorker"/>
/// calls <see cref="RunAsync"/> every hour.
///
/// Rows go <see cref="BatchSize"/> at a time, one statement and one transaction per batch. With
/// several app servers only the one holding the distributed lock runs; the others skip that hour.
/// Two runs overlapping anyway would only delete the same rows once.
/// </summary>
public class ExternalGuestCleanup : DomainService
{
    public const string LockName = "Dixels:ExternalGuestCleanup";
    public const int BatchSize = 500;

    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ISettingProvider _settingProvider;

    public ExternalGuestCleanup(
        IBookingRepository bookingRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IAbpDistributedLock distributedLock,
        ISettingProvider settingProvider)
    {
        _bookingRepository = bookingRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _distributedLock = distributedLock;
        _settingProvider = settingProvider;
    }

    /// <summary>
    /// Deletes every outside guest's row past the retention window; returns how many (0 when
    /// the cleanup is off, or another server is already doing it).
    /// </summary>
    public async Task<int> RunAsync()
    {
        // Read each run, so a changed setting applies from the next hour without a restart.
        var days = await _settingProvider.GetAsync(DixelsSettings.ExternalGuestRetentionDays, defaultValue: 90);
        if (days <= 0)
        {
            return 0;
        }

        await using var handle = await _distributedLock.TryAcquireAsync(LockName);
        if (handle is null)
        {
            return 0;
        }

        var cutoff = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero).AddDays(-days);
        var removed = await InBatchesAsync(() => _bookingRepository.DeleteExpiredExternalGuestsAsync(cutoff, BatchSize))
                      + await InBatchesAsync(() => _bookingRepository.DeleteExpiredSeriesExternalGuestsAsync(cutoff, BatchSize));

        if (removed > 0)
        {
            Logger.LogInformation("Deleted {Count} outside guests' details kept longer than {Days} days.", removed, days);
        }

        return removed;
    }

    /// <summary>Runs the delete a batch at a time, each in its own transaction, until a batch comes back short.</summary>
    private async Task<int> InBatchesAsync(Func<Task<int>> deleteBatchAsync)
    {
        var total = 0;
        while (true)
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var deleted = await deleteBatchAsync();
            await uow.CompleteAsync();

            total += deleted;
            if (deleted < BatchSize)
            {
                return total;
            }
        }
    }
}
