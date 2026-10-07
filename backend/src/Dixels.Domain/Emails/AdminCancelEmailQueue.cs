using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace Dixels.Emails;

/// <summary>
/// Turns an admin's cancels into one email per person. One admin action can announce several
/// <see cref="BookingsCancelledEvent"/>s (a round of 500 each, one per broken rule), so they're
/// gathered for the whole unit of work first: the first one also announces an
/// <see cref="AdminCancelEmailsDueEvent"/>, which ABP runs in a later round of the same save,
/// after every cancel of the action is in. That queues one <see cref="AdminCancelledEmailJob"/>
/// per person — in the same transaction, so a save that rolls back emails nobody.
///
/// A cancel in a later round still (one an event handler made, after the emails were
/// queued) starts a new gathering: that person can get a second email, never none.
/// </summary>
public class AdminCancelEmailQueue : ITransientDependency
{
    /// <summary>How many bookings an email lists; the rest are "…and N more".</summary>
    public const int ShownBookings = 20;

    private const string PendingItem = "Dixels.Emails.AdminCancels";

    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILocalEventBus _localEventBus;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public AdminCancelEmailQueue(
        IUnitOfWorkManager unitOfWorkManager,
        ILocalEventBus localEventBus,
        IBackgroundJobManager backgroundJobManager)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _localEventBus = localEventBus;
        _backgroundJobManager = backgroundJobManager;
    }

    /// <summary>Adds what an admin cancelled to this unit of work's emails.</summary>
    public async Task AddAsync(IReadOnlyCollection<Booking> cancelled)
    {
        if (cancelled.Count == 0)
        {
            return;
        }

        var unitOfWork = _unitOfWorkManager.Current;
        if (unitOfWork is null)
        {
            await EnqueueAsync(cancelled);
            return;
        }

        if (unitOfWork.Items.TryGetValue(PendingItem, out var pending))
        {
            ((AdminCancelEmailsDueEvent)pending).Bookings.AddRange(cancelled);
            return;
        }

        var due = new AdminCancelEmailsDueEvent(cancelled.ToList());
        unitOfWork.Items[PendingItem] = due;
        await _localEventBus.PublishAsync(due);
    }

    /// <summary>Queues the emails for everything gathered, one per person.</summary>
    public Task SendAsync(AdminCancelEmailsDueEvent due)
    {
        var unitOfWork = _unitOfWorkManager.Current;
        if (unitOfWork is not null && unitOfWork.Items.TryGetValue(PendingItem, out var pending) && pending == due)
        {
            unitOfWork.Items.Remove(PendingItem);
        }

        return EnqueueAsync(due.Bookings);
    }

    private async Task EnqueueAsync(IEnumerable<Booking> cancelled)
    {
        foreach (var owners in cancelled.DistinctBy(b => b.Id).GroupBy(b => b.UserId))
        {
            var soonest = owners.OrderBy(b => b.StartsAt).ThenBy(b => b.Id).ToList();
            await _backgroundJobManager.EnqueueAsync(new AdminCancelledEmailArgs
            {
                UserId = owners.Key,
                BookingIds = soonest.Take(ShownBookings).Select(b => b.Id).ToList(),
                Count = soonest.Count,
            });
        }
    }
}

/// <summary>
/// An admin action's cancels are all in: time to queue its emails (see
/// <see cref="AdminCancelEmailQueue"/>). Only the email code raises and handles it.
/// </summary>
public class AdminCancelEmailsDueEvent
{
    public List<Booking> Bookings { get; }

    public AdminCancelEmailsDueEvent(List<Booking> bookings)
    {
        Bookings = bookings;
    }
}
