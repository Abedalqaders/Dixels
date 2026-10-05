using System.Threading.Tasks;
using Dixels.Bookings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace Dixels.Emails;

/// <summary>
/// Emails the employee when something happens to their bookings (see BookingEvents). Admin
/// cancellations deliberately send nothing: the employee sees them in their calendar.
/// </summary>
public class BookingEmailHandler :
    ILocalEventHandler<BookingConfirmedEvent>,
    ILocalEventHandler<BookingSeriesConfirmedEvent>,
    ILocalEventHandler<BookingsCancelledEvent>,
    ILocalEventHandler<BookingReminderDueEvent>,
    ITransientDependency
{
    private readonly BookingEmails _bookingEmails;

    public BookingEmailHandler(BookingEmails bookingEmails)
    {
        _bookingEmails = bookingEmails;
    }

    public Task HandleEventAsync(BookingConfirmedEvent eventData) =>
        _bookingEmails.SendConfirmedAsync(eventData.Booking);

    public Task HandleEventAsync(BookingSeriesConfirmedEvent eventData) =>
        _bookingEmails.SendSeriesConfirmedAsync(eventData.Series, eventData.Bookings);

    public Task HandleEventAsync(BookingsCancelledEvent eventData) =>
        eventData.ByAdmin ? Task.CompletedTask : _bookingEmails.SendCancelledAsync(eventData.Bookings);

    public Task HandleEventAsync(BookingReminderDueEvent eventData) =>
        _bookingEmails.SendReminderAsync(eventData.Booking);
}
