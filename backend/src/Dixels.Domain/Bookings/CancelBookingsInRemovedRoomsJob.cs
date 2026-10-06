using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace Dixels.Bookings;

/// <summary>What <see cref="CancelBookingsInRemovedRoomsJob"/> cancels, for whom, and why.</summary>
[BackgroundJobName("Dixels.CancelBookingsInRemovedRooms")]
public class CancelBookingsInRemovedRoomsArgs
{
    /// <summary>The rooms removed (a room, or every room on a floor or in a building).</summary>
    public List<Guid> SpaceIds { get; set; } = new();

    /// <summary>The admin who removed them: the cancels are theirs.</summary>
    public Guid AdminId { get; set; }

    /// <summary>What employees see, already in the admin's language ("The building was removed").</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Cancels the upcoming bookings in rooms an admin removed, after the delete has saved —
/// so a building with thousands of bookings deletes at once, and a cancel that fails is
/// retried by the job queue instead of undoing the delete.
///
/// A batch at a time, each saved on its own: a retry picks up where it stopped, as cancelled
/// bookings no longer match. Rooms restored before it runs are left alone (their bookings
/// stand, as if never removed).
/// </summary>
public class CancelBookingsInRemovedRoomsJob : AsyncBackgroundJob<CancelBookingsInRemovedRoomsArgs>, ITransientDependency
{
    public const int BatchSize = 500;

    private readonly BookingImpactChecker _checker;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public CancelBookingsInRemovedRoomsJob(
        BookingImpactChecker checker,
        IRepository<Space, Guid> spaceRepository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _checker = checker;
        _spaceRepository = spaceRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public override async Task ExecuteAsync(CancelBookingsInRemovedRoomsArgs args)
    {
        var spaceIds = await StillRemovedAsync(args.SpaceIds);
        var previous = new HashSet<Guid>();

        while (true)
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true);
            var batch = await _checker.FindUpcomingAsync(spaceIds, BatchSize);

            // Each batch cancelled drops out of the next. One that comes back again means
            // nothing was cancelled: stop rather than ask for the same bookings forever.
            if (batch.Any(b => previous.Contains(b.Id)))
            {
                Logger.LogWarning(
                    "Stopped cancelling bookings in removed rooms: the last batch of {Count} wasn't cancelled.", batch.Count);
                return;
            }

            await _checker.CancelAsAdminAsync(batch, args.AdminId, _ => args.Reason);
            await uow.CompleteAsync();

            if (batch.Count < BatchSize)
            {
                return;
            }

            previous = batch.Select(b => b.Id).ToHashSet();
        }
    }

    /// <summary>The rooms that are still removed: the soft-delete filter hides those, so any it finds were restored.</summary>
    private async Task<List<Guid>> StillRemovedAsync(List<Guid> spaceIds)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        var restored = (await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id))).Select(s => s.Id).ToHashSet();
        await uow.CompleteAsync();
        return spaceIds.Where(id => !restored.Contains(id)).ToList();
    }
}
