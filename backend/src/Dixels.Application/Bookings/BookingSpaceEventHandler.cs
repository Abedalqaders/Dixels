using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace Dixels.Bookings;

/// <summary>
/// Cancels the upcoming bookings in rooms that were deleted (see SpaceManagementEvents): a
/// booking can't be kept in a room that no longer exists. The employee sees why on their
/// calendar ("The room was removed", or its floor or building).
/// </summary>
public class BookingSpaceEventHandler :
    ILocalEventHandler<SpaceDeletedEvent>,
    ILocalEventHandler<FloorDeletedEvent>,
    ILocalEventHandler<BuildingDeletedEvent>,
    ITransientDependency
{
    private readonly BookingImpactService _bookingImpact;

    public BookingSpaceEventHandler(BookingImpactService bookingImpact)
    {
        _bookingImpact = bookingImpact;
    }

    public Task HandleEventAsync(SpaceDeletedEvent eventData) =>
        CancelUpcomingAsync(eventData.SpaceIds, eventData.ByUserId, "Dixels:Bookings:CancelReason:SpaceRemoved");

    public Task HandleEventAsync(FloorDeletedEvent eventData) =>
        CancelUpcomingAsync(eventData.SpaceIds, eventData.ByUserId, "Dixels:Bookings:CancelReason:FloorRemoved");

    public Task HandleEventAsync(BuildingDeletedEvent eventData) =>
        CancelUpcomingAsync(eventData.SpaceIds, eventData.ByUserId, "Dixels:Bookings:CancelReason:BuildingRemoved");

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
