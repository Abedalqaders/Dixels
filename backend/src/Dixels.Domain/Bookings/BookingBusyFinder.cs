using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

namespace Dixels.Bookings;

/// <summary>
/// A time someone is taken: their own confirmed booking, or a meeting they accepted (an
/// invite still pending or declined doesn't count, like a tentative one in Outlook).
/// </summary>
public readonly record struct BusySlot(Guid UserId, TimeRange Range, bool IsAcceptedInvite);

/// <summary>
/// Who among some colleagues is busy at some times — for the guest picker's "Busy then" tag.
/// Only ever a warning: nobody is kept from being invited. It says that someone is busy,
/// never with what.
/// </summary>
public class BookingBusyFinder : DomainService
{
    private readonly IBookingRepository _bookingRepository;

    public BookingBusyFinder(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    /// <summary>
    /// For each person busy at one or more of <paramref name="slots"/> (a booking's one time,
    /// or a series' dates), on how many of them — people never busy are left out. Bookings in
    /// <paramref name="exceptBookingIds"/> don't count: the ones being edited, whose own
    /// guests are of course "busy" with them. Two indexed queries for the whole lot, however
    /// many slots; the overlap per slot is worked out here.
    /// </summary>
    public async Task<Dictionary<Guid, int>> CountBusyDatesAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyList<TimeRange> slots, IReadOnlyCollection<Guid>? exceptBookingIds = null)
    {
        if (userIds.Count == 0 || slots.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var busy = await _bookingRepository.GetBusyAsync(
            userIds, slots.Min(s => s.Start), slots.Max(s => s.End), exceptBookingIds ?? Array.Empty<Guid>());

        return busy
            .GroupBy(b => b.UserId)
            .Select(g => (UserId: g.Key, Dates: slots.Count(s => g.Any(b => b.Range.Overlaps(s.Start, s.End)))))
            .Where(x => x.Dates > 0)
            .ToDictionary(x => x.UserId, x => x.Dates);
    }
}
