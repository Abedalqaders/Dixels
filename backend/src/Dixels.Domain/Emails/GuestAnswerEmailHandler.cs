using System.Threading.Tasks;
using Dixels.Bookings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace Dixels.Emails;

/// <summary>
/// The booker's email when a guest declines — the one place it's sent from, so it goes however
/// the answer came in (the app, the guest's link, later their mail app). Only when the answer
/// turns to Declined: a guest changing their mind back and forth doesn't flood the booker, and an
/// accept sends nothing.
/// </summary>
public class GuestAnswerEmailHandler : ILocalEventHandler<BookingInviteeRespondedEvent>, ITransientDependency
{
    private readonly BookingEmails _bookingEmails;

    public GuestAnswerEmailHandler(BookingEmails bookingEmails)
    {
        _bookingEmails = bookingEmails;
    }

    public Task HandleEventAsync(BookingInviteeRespondedEvent eventData) =>
        eventData.Status == InviteeResponseStatus.Declined && eventData.PreviousStatus != InviteeResponseStatus.Declined
            ? _bookingEmails.SendGuestDeclinedAsync(eventData)
            : Task.CompletedTask;
}
