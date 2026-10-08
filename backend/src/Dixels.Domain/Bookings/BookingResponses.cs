using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;

namespace Dixels.Bookings;

/// <summary>
/// A guest answering an invitation (accept / decline). In the app only a current colleague guest
/// may (<see cref="BookingAccess"/>): the organiser is told there's nothing to answer (403), anyone
/// else that the booking doesn't exist. Through their own link (<see cref="GuestLinks"/>) any
/// guest may, outsiders too: the link is the proof. Either way the same rules apply. An answer
/// changes nothing else — not the head count, not the booking: if everyone declines, the booking
/// still stands and the owner decides.
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
    /// A colleague guest's answer to one date. It can be changed any number of times until the
    /// meeting starts; after that, or once cancelled, <see cref="DixelsDomainErrorCodes.BookingResponseClosed"/>.
    /// </summary>
    public async Task<Booking> RespondAsync(Guid bookingId, Guid userId, InviteeResponseStatus status)
    {
        var booking = await _bookingRepository.GetAsync(bookingId);
        await _bookingAccess.EnsureAsync(booking, userId, DixelsDomainErrorCodes.BookingOrganiserCannotRespond, BookingRole.Guest);

        await AnswerDateAsync(booking, new Invitee(userId, null, null), status);
        return booking;
    }

    /// <summary>
    /// A colleague guest's answer for a whole series: the series' own list and every upcoming
    /// confirmed date, overwriting answers given to single dates. Dates under way, over or
    /// cancelled keep theirs. With no upcoming date left, <see cref="DixelsDomainErrorCodes.BookingResponseClosed"/>.
    /// </summary>
    public async Task RespondToSeriesAsync(Guid seriesId, Guid userId, InviteeResponseStatus status)
    {
        var series = await _seriesRepository.GetAsync(seriesId);
        await _bookingAccess.EnsureSeriesAsync(series, userId, DixelsDomainErrorCodes.BookingOrganiserCannotRespond, BookingRole.Guest);

        await AnswerSeriesAsync(series, new Invitee(userId, null, null), status);
    }

    /// <summary>
    /// The answer of a guest already proven by how it came in (their own link, found by
    /// <see cref="GuestLinks.ResolveAsync"/>; later their mail app's reply): no access check. A
    /// booking row's target answers that date (a single date of a series too); a series row's
    /// the whole series, as <see cref="RespondToSeriesAsync"/>.
    /// </summary>
    public async Task RespondAsGuestAsync(GuestLinkTarget target, InviteeResponseStatus status)
    {
        var guest = target.Row.ToInvitee();
        if (target.Booking is not null)
        {
            await AnswerDateAsync(target.Booking, guest, status);
        }
        else
        {
            await AnswerSeriesAsync(target.Series!, guest, status);
        }
    }

    /// <summary>Whether the link's meeting still takes answers (the page shows "closed" otherwise).</summary>
    public async Task<bool> IsOpenAsync(GuestLinkTarget target)
    {
        var now = Now();
        if (target.Booking is not null)
        {
            return target.Booking.IsOpenForAnswers(now);
        }

        var seriesId = target.Series!.Id;
        return await _bookingRepository.AnyAsync(b => b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed && b.StartsAt > now);
    }

    private async Task AnswerDateAsync(Booking booking, Invitee guest, InviteeResponseStatus status)
    {
        var previous = booking.Respond(guest.Key, status, Now());
        await _bookingRepository.UpdateAsync(booking);
        await _localEventBus.PublishAsync(new BookingInviteeRespondedEvent(booking.Id, null, guest, status, previous));
    }

    private async Task AnswerSeriesAsync(BookingSeries series, Invitee guest, InviteeResponseStatus status)
    {
        var now = Now();
        var seriesId = series.Id;
        var upcoming = await _bookingRepository.GetListAsync(
            b => b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed && b.StartsAt > now,
            includeDetails: true);
        if (upcoming.Count == 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingResponseClosed);
        }

        var previous = series.Respond(guest.Key, status, now);
        // Every upcoming date carries the series' list (T4 keeps them in step), each with its own
        // copy of the guest's row; a date that somehow doesn't have them is left alone rather
        // than failing the whole answer.
        foreach (var booking in upcoming.Where(b => b.Invitees.Any(i => i.ToInvitee().Key == guest.Key)))
        {
            booking.Respond(guest.Key, status, now);
        }

        await _seriesRepository.UpdateAsync(series);
        await _bookingRepository.UpdateManyAsync(upcoming);
        await _localEventBus.PublishAsync(new BookingInviteeRespondedEvent(null, seriesId, guest, status, previous));
    }
}
