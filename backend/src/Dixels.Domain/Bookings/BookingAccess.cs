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
    /// reveals nothing; someone with a role that isn't allowed — a guest asking for an
    /// organiser-only action, or the organiser for a guest-only one like answering — is told so
    /// plainly (403, <see cref="DixelsDomainErrorCodes.BookingOrganiserOnly"/> or the code
    /// given), since they already know it exists.
    /// </summary>
    public Task EnsureAsync(Booking booking, Guid userId, params BookingRole[] allowed) =>
        EnsureAsync(booking, userId, DixelsDomainErrorCodes.BookingOrganiserOnly, allowed);

    /// <summary>The same, with the error code a refused (known) role gets — e.g. one that names the action.</summary>
    public async Task EnsureAsync(Booking booking, Guid userId, string refusedCode, params BookingRole[] allowed)
    {
        var role = await GetRoleAsync(booking, userId);
        if (allowed.Contains(role))
        {
            return;
        }

        if (role == BookingRole.None)
        {
            throw new EntityNotFoundException(typeof(Booking), booking.Id);
        }

        throw new BusinessException(refusedCode);
    }
}
