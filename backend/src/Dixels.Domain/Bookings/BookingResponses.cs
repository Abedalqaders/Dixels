using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;

namespace Dixels.Bookings;

/// <summary>
/// A colleague guest answering an invitation (accept / decline). Only a current guest may
/// (<see cref="BookingAccess"/>): the organiser is told there's nothing to answer (403), anyone
/// else that the booking doesn't exist. An answer changes nothing else — not the head count,
/// not the booking: if everyone declines, the booking still stands and the owner decides.
/// </summary>
public class BookingResponses : DomainService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly BookingAccess _bookingAccess;
    private readonly ILocalEventBus _localEventBus;

    public BookingResponses(IBookingRepository bookingRepository, BookingAccess bookingAccess, ILocalEventBus localEventBus)
    {
        _bookingRepository = bookingRepository;
        _bookingAccess = bookingAccess;
        _localEventBus = localEventBus;
    }

    /// <summary>
    /// The guest's answer to one date. It can be changed any number of times until the meeting
    /// starts; after that, or once cancelled, <see cref="DixelsDomainErrorCodes.BookingResponseClosed"/>.
    /// </summary>
    public async Task<Booking> RespondAsync(Guid bookingId, Guid userId, InviteeResponseStatus status)
    {
        var booking = await _bookingRepository.GetAsync(bookingId);
        await _bookingAccess.EnsureAsync(booking, userId, DixelsDomainErrorCodes.BookingOrganiserCannotRespond, BookingRole.Guest);

        booking.Respond(userId, status, new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero));
        await _bookingRepository.UpdateAsync(booking);
        await _localEventBus.PublishAsync(new BookingInviteeRespondedEvent(booking.Id, null, userId, status));
        return booking;
    }
}
