using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
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
    private readonly IRepository<BookingSeries, Guid> _seriesRepository;
    private readonly BookingAccess _bookingAccess;
    private readonly ILocalEventBus _localEventBus;

    public BookingResponses(
        IBookingRepository bookingRepository,
        IRepository<BookingSeries, Guid> seriesRepository,
        BookingAccess bookingAccess,
        ILocalEventBus localEventBus)
    {
        _bookingRepository = bookingRepository;
        _seriesRepository = seriesRepository;
        _bookingAccess = bookingAccess;
        _localEventBus = localEventBus;
    }

    private DateTimeOffset Now() => new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);

    /// <summary>
    /// The guest's answer to one date. It can be changed any number of times until the meeting
    /// starts; after that, or once cancelled, <see cref="DixelsDomainErrorCodes.BookingResponseClosed"/>.
    /// </summary>
    public async Task<Booking> RespondAsync(Guid bookingId, Guid userId, InviteeResponseStatus status)
    {
        var booking = await _bookingRepository.GetAsync(bookingId);
        await _bookingAccess.EnsureAsync(booking, userId, DixelsDomainErrorCodes.BookingOrganiserCannotRespond, BookingRole.Guest);

        booking.Respond(userId, status, Now());
        await _bookingRepository.UpdateAsync(booking);
        await _localEventBus.PublishAsync(new BookingInviteeRespondedEvent(booking.Id, null, userId, status));
        return booking;
    }

    /// <summary>
    /// The guest's answer for a whole series: the series' own list and every upcoming confirmed
    /// date, overwriting answers given to single dates. Dates under way, over or cancelled keep
    /// theirs. With no upcoming date left, <see cref="DixelsDomainErrorCodes.BookingResponseClosed"/>.
    /// </summary>
    public async Task RespondToSeriesAsync(Guid seriesId, Guid userId, InviteeResponseStatus status)
    {
        var series = await _seriesRepository.GetAsync(seriesId);
        await _bookingAccess.EnsureSeriesAsync(series, userId, DixelsDomainErrorCodes.BookingOrganiserCannotRespond, BookingRole.Guest);

        var now = Now();
        var upcoming = await _bookingRepository.GetListAsync(
            b => b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed && b.StartsAt > now,
            includeDetails: true);
        if (upcoming.Count == 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingResponseClosed);
        }

        series.Respond(userId, status, now);
        // Every upcoming date carries the series' list (T4 keeps them in step); a date that
        // somehow doesn't have them is left alone rather than failing the whole answer.
        foreach (var booking in upcoming.Where(b => b.Invitees.Any(i => i.UserId == userId)))
        {
            booking.Respond(userId, status, now);
        }

        await _seriesRepository.UpdateAsync(series);
        await _bookingRepository.UpdateManyAsync(upcoming);
        await _localEventBus.PublishAsync(new BookingInviteeRespondedEvent(null, seriesId, userId, status));
    }
}
