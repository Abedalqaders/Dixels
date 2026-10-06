using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace Dixels.Bookings;

/// <summary>
/// Keeps bookings in step with what happens to rooms (see SpaceManagementEvents):
/// <list type="bullet">
/// <item>a room, floor or building deleted: its upcoming bookings are cancelled ("The room was
/// removed", or its floor or building);</item>
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

    public BookingSpaceEventHandler(BookingImpactService bookingImpact, ConstraintResolver constraintResolver)
    {
        _bookingImpact = bookingImpact;
        _constraintResolver = constraintResolver;
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

        var broken = await FindNoLongerFittingAsync(eventData.BuildingId, eventData.SpaceIds);
        await _bookingImpact.CancelForRuleChangeAsync(broken, eventData.ByUserId);
    }

    public async Task HandleEventAsync(ClosureCreatedEvent eventData)
    {
        if (!eventData.CancelAffected)
        {
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

        // Found by room id, so rooms that are already deleted are no obstacle.
        var upcoming = await _bookingImpact.Checker.FindUpcomingAsync(spaceIds);
        var reason = _bookingImpact.Text(reasonKey);
        await _bookingImpact.Checker.CancelAsAdminAsync(upcoming, adminId, _ => reason);
    }
}
