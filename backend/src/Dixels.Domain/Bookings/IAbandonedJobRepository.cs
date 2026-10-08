using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dixels.Bookings;

/// <summary>
/// Background jobs ABP gave up on (IsAbandoned): they stay in AbpBackgroundJobs for good, with
/// their arguments. Used by <see cref="ExternalGuestCleanup"/> to delete old email jobs, whose
/// arguments are a rendered email (names, addresses, the calendar invite).
/// </summary>
public interface IAbandonedJobRepository
{
    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> abandoned jobs named one of
    /// <paramref name="jobNames"/> and created before <paramref name="createdBefore"/>, in one
    /// statement. Returns how many went.
    /// </summary>
    Task<int> DeleteAbandonedAsync(
        IReadOnlyCollection<string> jobNames,
        DateTime createdBefore,
        int batchSize,
        CancellationToken cancellationToken = default);
}
