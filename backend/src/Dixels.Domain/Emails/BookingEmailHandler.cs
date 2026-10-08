using System.Threading.Tasks;
using Dixels.Bookings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace Dixels.Emails;

/// <summary>
/// Emails the employee when something happens to their bookings (see BookingEvents). Admin
/// cancellations are gathered per admin action and sent as one email per person (see
/// <see cref="AdminCancelEmailQueue"/>).
/// </summary>
public class BookingEmailHandler :
    ILocalEventHandler<BookingConfirmedEvent>,
    ILocalEventHandler<BookingSeriesConfirmedEvent>,
    ILocalEventHandler<BookingsCancelledEvent>,
    ILocalEventHandler<AdminCancelEmailsDueEvent>,
    ILocalEventHandler<BookingReminderDueEvent>,
    ILocalEventHandler<BookingInviteesChangedEvent>,
    ITransientDependency
{
    private readonly BookingEmails _bookingEmails;
    private readonly AdminCancelEmailQueue _adminCancels;

    public BookingEmailHandler(BookingEmails bookingEmails, AdminCancelEmailQueue adminCancels)
    {
        _bookingEmails = bookingEmails;
        _adminCancels = adminCancels;
    }

    public async Task HandleEventAsync(BookingConfirmedEvent eventData)
    {
        await _bookingEmails.SendConfirmedAsync(eventData.Booking);
        await _bookingEmails.SendInvitesAsync(eventData.Booking);
    }

    public async Task HandleEventAsync(BookingSeriesConfirmedEvent eventData)
    {
        await _bookingEmails.SendSeriesConfirmedAsync(eventData.Series, eventData.Bookings);
        await _bookingEmails.SendSeriesInvitesAsync(eventData.Series, eventData.Bookings);
    }

    public async Task HandleEventAsync(BookingsCancelledEvent eventData)
    {
        if (eventData.ByAdmin)
        {
            await _adminCancels.AddAsync(eventData.Bookings);
            return;
        }

        var guestsTold = await _bookingEmails.SendGuestCancelsAsync(eventData.Bookings);
        await _bookingEmails.SendCancelledAsync(eventData.Bookings, guestsTold);
    }

    public Task HandleEventAsync(AdminCancelEmailsDueEvent eventData) =>
        _adminCancels.SendAsync(eventData);

    public Task HandleEventAsync(BookingInviteesChangedEvent eventData) =>
        _bookingEmails.SendGuestChangesAsync(eventData);

    public Task HandleEventAsync(BookingReminderDueEvent eventData) =>
        _bookingEmails.SendReminderAsync(eventData.Booking);
}
