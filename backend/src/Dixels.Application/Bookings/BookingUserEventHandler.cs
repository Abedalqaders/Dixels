using System;
using System.Threading.Tasks;
using Dixels.Users;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace Dixels.Bookings;

/// <summary>
/// Releases a person's upcoming bookings when their account changes (see UserEvents): the
/// rooms would otherwise be held for someone who can't, or won't, come. They also come off
/// the upcoming meetings they were invited to, silently (see
/// <see cref="BookingManager.RemoveGuestEverywhereAsync"/>).
/// </summary>
public class BookingUserEventHandler :
    ILocalEventHandler<UserDeactivatedEvent>,
    ILocalEventHandler<UserDeletedEvent>,
    ILocalEventHandler<UserMovedBuildingEvent>,
    ITransientDependency
{
    private readonly BookingImpactService _bookingImpact;
    private readonly BookingManager _bookingManager;

    public BookingUserEventHandler(BookingImpactService bookingImpact, BookingManager bookingManager)
    {
        _bookingImpact = bookingImpact;
        _bookingManager = bookingManager;
    }

    public Task HandleEventAsync(UserDeactivatedEvent eventData) =>
        LeaveAsync(eventData.UserId, eventData.ByUserId, "Dixels:Bookings:CancelReason:AccountDeactivated");

    public Task HandleEventAsync(UserDeletedEvent eventData) =>
        LeaveAsync(eventData.UserId, eventData.ByUserId, "Dixels:Bookings:CancelReason:AccountRemoved");

    // They can only book, and be invited as a colleague, in one building: the old one's go.
    public async Task HandleEventAsync(UserMovedBuildingEvent eventData)
    {
        await _bookingImpact.CancelOnMoveAsync(eventData.UserId, eventData.FromBuildingId, eventData.ToBuildingId, eventData.ByUserId);
        if (eventData.FromBuildingId is { } from && from != eventData.ToBuildingId)
        {
            await _bookingManager.RemoveGuestEverywhereAsync(eventData.UserId, onlyInBuildingId: from);
        }
    }

    private async Task LeaveAsync(Guid userId, Guid adminId, string reasonKey)
    {
        await _bookingImpact.CancelAllAsync(await _bookingImpact.UpcomingForUserAsync(userId), adminId, _bookingImpact.Text(reasonKey));
        await _bookingManager.RemoveGuestEverywhereAsync(userId);
    }
}
