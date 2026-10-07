using System;
using System.Threading.Tasks;
using Dixels.Users;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace Dixels.Bookings;

/// <summary>
/// Releases a person's upcoming bookings when their account changes (see UserEvents): the
/// rooms would otherwise be held for someone who can't, or won't, come.
/// </summary>
public class BookingUserEventHandler :
    ILocalEventHandler<UserDeactivatedEvent>,
    ILocalEventHandler<UserDeletedEvent>,
    ILocalEventHandler<UserMovedBuildingEvent>,
    ITransientDependency
{
    private readonly BookingImpactService _bookingImpact;

    public BookingUserEventHandler(BookingImpactService bookingImpact)
    {
        _bookingImpact = bookingImpact;
    }

    public Task HandleEventAsync(UserDeactivatedEvent eventData) =>
        CancelUpcomingAsync(eventData.UserId, eventData.ByUserId, "Dixels:Bookings:CancelReason:AccountDeactivated");

    public Task HandleEventAsync(UserDeletedEvent eventData) =>
        CancelUpcomingAsync(eventData.UserId, eventData.ByUserId, "Dixels:Bookings:CancelReason:AccountRemoved");

    // They can only book in one building, so their bookings in the old one go.
    public Task HandleEventAsync(UserMovedBuildingEvent eventData) =>
        _bookingImpact.CancelOnMoveAsync(eventData.UserId, eventData.FromBuildingId, eventData.ToBuildingId, eventData.ByUserId);

    private async Task CancelUpcomingAsync(Guid userId, Guid adminId, string reasonKey)
    {
        await _bookingImpact.CancelAllAsync(await _bookingImpact.UpcomingForUserAsync(userId), adminId, _bookingImpact.Text(reasonKey));
    }
}
