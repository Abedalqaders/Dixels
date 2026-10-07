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
    ITransientDependency
{
    private readonly BookingEmails _bookingEmails;
    private readonly AdminCancelEmailQueue _adminCancels;

    public BookingEmailHandler(BookingEmails bookingEmails, AdminCancelEmailQueue adminCancels)
    {
        _bookingEmails = bookingEmails;
        _adminCancels = adminCancels;
    }

    public Task HandleEventAsync(BookingConfirmedEvent eventData) =>
        _bookingEmails.SendConfirmedAsync(eventData.Booking);

    public Task HandleEventAsync(BookingSeriesConfirmedEvent eventData) =>
        _bookingEmails.SendSeriesConfirmedAsync(eventData.Series, eventData.Bookings);

    public Task HandleEventAsync(BookingsCancelledEvent eventData) =>
        eventData.ByAdmin ? _adminCancels.AddAsync(eventData.Bookings) : _bookingEmails.SendCancelledAsync(eventData.Bookings);

    public Task HandleEventAsync(AdminCancelEmailsDueEvent eventData) =>
        _adminCancels.SendAsync(eventData);

    public Task HandleEventAsync(BookingReminderDueEvent eventData) =>
        _bookingEmails.SendReminderAsync(eventData.Booking);
}
