using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace Dixels.Bookings;

/// <summary>
/// Keeps bookings in step with what happens to rooms (see SpaceManagementEvents):
/// <list type="bullet">
/// <item>a room, floor or building deleted: its upcoming bookings are cancelled ("The room was
/// removed", or its floor or building) by a background job, queued with the delete (and
/// dropped with it if the delete rolls back);</item>
/// <item>rules changed or a closure added, and the admin chose to cancel what no longer fits:
/// the upcoming bookings in those rooms are checked against the saved rules and closures, and
/// the ones that break them are cancelled, each with the rule it breaks or the closure's
/// reason.</item>
/// </list>
/// Every cancel is an admin's, so the employee sees it struck through on their calendar.
/// </summary>
public class BookingSpaceEventHandler :
    ILocalEventHandler<SpaceDeletedEvent>,
    ILocalEventHandler<FloorDeletedEvent>,
    ILocalEventHandler<BuildingDeletedEvent>,
    ILocalEventHandler<SpaceRulesChangedEvent>,
    ILocalEventHandler<ClosureCreatedEvent>,
    ITransientDependency
{
    private readonly BookingImpactService _bookingImpact;
    private readonly ConstraintResolver _constraintResolver;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public BookingSpaceEventHandler(
        BookingImpactService bookingImpact,
        ConstraintResolver constraintResolver,
        IBackgroundJobManager backgroundJobManager)
    {
        _bookingImpact = bookingImpact;
        _constraintResolver = constraintResolver;
        _backgroundJobManager = backgroundJobManager;
    }

    public Task HandleEventAsync(SpaceDeletedEvent eventData) =>
        CancelUpcomingAsync(eventData.SpaceIds, eventData.ByUserId, "Dixels:Bookings:CancelReason:SpaceRemoved");

    public Task HandleEventAsync(FloorDeletedEvent eventData) =>
        CancelUpcomingAsync(eventData.SpaceIds, eventData.ByUserId, "Dixels:Bookings:CancelReason:FloorRemoved");

    public Task HandleEventAsync(BuildingDeletedEvent eventData) =>
        CancelUpcomingAsync(eventData.SpaceIds, eventData.ByUserId, "Dixels:Bookings:CancelReason:BuildingRemoved");

    public async Task HandleEventAsync(SpaceRulesChangedEvent eventData)
    {
        if (!eventData.CancelAffected)
        {
            return;
        }

        // The save already checked the rooms (to tell the admin how many): cancel what it found
        // rather than checking every upcoming booking a second time.
        if (eventData.Affected is { } affected)
        {
            await _bookingImpact.CancelForRuleChangeAsync(affected, eventData.ByUserId);
            return;
        }

        var broken = await FindNoLongerFittingAsync(eventData.BuildingId, eventData.SpaceIds);
        await _bookingImpact.CancelForRuleChangeAsync(broken, eventData.ByUserId);
    }

    public async Task HandleEventAsync(ClosureCreatedEvent eventData)
    {
        if (!eventData.CancelAffected)
        {
            return;
        }

        if (eventData.Affected is { } affected)
        {
            await _bookingImpact.CancelAllAsync(affected, eventData.ByUserId, eventData.Reason);
            return;
        }

        // The closure is saved, so the check reads it with the room's other closures.
        var broken = await FindNoLongerFittingAsync(eventData.BuildingId, eventData.SpaceIds);
        if (broken.Count > 0)
        {
            await _bookingImpact.CancelAllAsync(broken, eventData.ByUserId, eventData.Reason);
        }
    }

    /// <summary>The upcoming bookings in these rooms that their saved rules and closures now reject.</summary>
    private async Task<IReadOnlyList<BookingImpact>> FindNoLongerFittingAsync(Guid buildingId, IReadOnlyList<Guid> spaceIds)
    {
        var (building, rooms) = await _bookingImpact.RoomsAsync(buildingId, spaceIds);
        if (building is null || rooms.Count == 0)
        {
            return Array.Empty<BookingImpact>();
        }

        return await _bookingImpact.Checker.FindNoLongerFittingAsync(
            building, rooms, (space, floor) => _constraintResolver.Resolve(building, floor, space));
    }

    private async Task CancelUpcomingAsync(IReadOnlyList<Guid> spaceIds, Guid adminId, string reasonKey)
    {
        if (spaceIds.Count == 0)
        {
            return;
        }

        // Queued in the delete's own unit of work (the job store saves with it), so it runs
        // only once the delete is saved. The reason is put in words now, while the admin's
        // language is known.
        await _backgroundJobManager.EnqueueAsync(new CancelBookingsInRemovedRoomsArgs
        {
            SpaceIds = spaceIds.ToList(),
            AdminId = adminId,
            Reason = _bookingImpact.Text(reasonKey),
        });
    }
}
