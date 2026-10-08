using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Services;

namespace Dixels.Bookings;

/// <summary>Who someone is to one booking.</summary>
public enum BookingRole
{
    /// <summary>Not theirs and not invited: to them the booking doesn't exist.</summary>
    None,

    /// <summary>The organiser, who booked it.</summary>
    Owner,

    /// <summary>A colleague invited to it (a current guest row).</summary>
    Guest,
}

/// <summary>
/// The one place that decides what someone may do with a booking — resource-based
/// authorization, as ASP.NET Core calls it, kept as a plain domain service. Every booking
/// endpoint that reads or changes one booking goes through <see cref="EnsureAsync(Booking, Guid, BookingRole[])"/>
/// rather than comparing user ids itself: reading (Owner, Guest), cancelling and editing the
/// guests (Owner), answering an invitation (Guest).
///
/// Not the same as <see cref="BookingAccessChecker"/>, which decides where someone may book.
/// </summary>
public class BookingAccess : DomainService
{
    private readonly IBookingRepository _bookingRepository;

    public BookingAccess(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    /// <summary>
    /// The person's role: Owner straight from the loaded booking, at no cost; Guest only when
    /// they aren't the owner, with one small query on the guest rows' unique index.
    /// </summary>
    public async Task<BookingRole> GetRoleAsync(Booking booking, Guid userId)
    {
        if (booking.UserId == userId)
        {
            return BookingRole.Owner;
        }

        return await _bookingRepository.IsInviteeAsync(booking.Id, userId) ? BookingRole.Guest : BookingRole.None;
    }

    /// <summary>
    /// Lets the call through when the person's role is one of <paramref name="allowed"/>.
    /// Otherwise: someone with no role is told the booking doesn't exist (404), so a guessed id
    /// reveals nothing; a guest asking for an organiser-only action is told so plainly (403,
    /// <see cref="DixelsDomainErrorCodes.BookingOrganiserOnly"/>) — they already know it exists.
    /// </summary>
    public Task EnsureAsync(Booking booking, Guid userId, params BookingRole[] allowed) =>
        EnsureAsync(booking, userId, DixelsDomainErrorCodes.BookingOrganiserOnly, allowed);

    /// <summary>The same, with the error code a refused guest gets — e.g. one that names the action.</summary>
    public async Task EnsureAsync(Booking booking, Guid userId, string guestRefusedCode, params BookingRole[] allowed)
    {
        Ensure(await GetRoleAsync(booking, userId), allowed, guestRefusedCode, typeof(Booking), booking.Id);
    }

    /// <summary>
    /// The person's role in a whole series, the way <see cref="GetRoleAsync"/> works for one
    /// booking: Owner from the series itself, at no cost; Guest when they're on the series' own
    /// guest list, with one small query on its unique index.
    /// </summary>
    public async Task<BookingRole> GetSeriesRoleAsync(BookingSeries series, Guid userId)
    {
        if (series.UserId == userId)
        {
            return BookingRole.Owner;
        }

        return await _bookingRepository.IsSeriesInviteeAsync(series.Id, userId) ? BookingRole.Guest : BookingRole.None;
    }

    /// <summary>The rule of <see cref="EnsureAsync(Booking, Guid, BookingRole[])"/> for a whole series: 404 for no role, 403 for a refused guest.</summary>
    public Task EnsureSeriesAsync(BookingSeries series, Guid userId, params BookingRole[] allowed) =>
        EnsureSeriesAsync(series, userId, DixelsDomainErrorCodes.BookingOrganiserOnly, allowed);

    /// <summary>The same, with the error code a refused guest gets.</summary>
    public async Task EnsureSeriesAsync(BookingSeries series, Guid userId, string guestRefusedCode, params BookingRole[] allowed)
    {
        Ensure(await GetSeriesRoleAsync(series, userId), allowed, guestRefusedCode, typeof(BookingSeries), series.Id);
    }

    private static void Ensure(BookingRole role, BookingRole[] allowed, string guestRefusedCode, Type entityType, Guid id)
    {
        if (allowed.Contains(role))
        {
            return;
        }

        if (role == BookingRole.None)
        {
            throw new EntityNotFoundException(entityType, id);
        }

        throw new BusinessException(guestRefusedCode);
    }
}
