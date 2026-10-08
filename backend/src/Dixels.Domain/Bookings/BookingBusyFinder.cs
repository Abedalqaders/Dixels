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
/// One person's busy times within a booking's time (or a series' dates): each cut to the
/// slot it overlaps and merged, earliest first — times only, never with what — and on how many
/// of the slots that is.
/// </summary>
public sealed record PersonBusy(int Dates, IReadOnlyList<TimeRange> Times);

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
    /// or a series' dates), when — cut to each slot — and on how many of them; people never
    /// busy are left out. Bookings in <paramref name="exceptBookingIds"/> don't count: the ones
    /// being edited, whose own guests are of course "busy" with them. Two indexed queries for
    /// the whole lot, however many slots; the overlap per slot is worked out here.
    /// </summary>
    public async Task<Dictionary<Guid, PersonBusy>> FindBusyAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyList<TimeRange> slots, IReadOnlyCollection<Guid>? exceptBookingIds = null)
    {
        if (userIds.Count == 0 || slots.Count == 0)
        {
            return new Dictionary<Guid, PersonBusy>();
        }

        var busy = await _bookingRepository.GetBusyAsync(
            userIds, slots.Min(s => s.Start), slots.Max(s => s.End), exceptBookingIds ?? Array.Empty<Guid>());

        var result = new Dictionary<Guid, PersonBusy>();
        foreach (var person in busy.GroupBy(b => b.UserId))
        {
            var dates = 0;
            var times = new List<TimeRange>();
            foreach (var slot in slots.OrderBy(s => s.Start))
            {
                var within = Merge(person.Select(b => b.Range.ClipTo(slot)).OfType<TimeRange>());
                if (within.Count > 0)
                {
                    dates++;
                    times.AddRange(within);
                }
            }

            if (dates > 0)
            {
                result[person.Key] = new PersonBusy(dates, times);
            }
        }

        return result;
    }

    /// <summary>Overlapping or touching ranges joined into one (an own booking and an accepted meeting back to back read as one busy time).</summary>
    private static List<TimeRange> Merge(IEnumerable<TimeRange> ranges)
    {
        var merged = new List<TimeRange>();
        foreach (var range in ranges.OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = new TimeRange(last.Start, range.End > last.End ? range.End : last.End);
            }
            else
            {
                merged.Add(range);
            }
        }

        return merged;
    }
}

/// <summary>
/// Edit guests' answer: how many dates were checked (1 for one booking), who is busy on them
/// and when, and the building's clock to show those times on.
/// </summary>
public sealed record BusyGuests(int Dates, IReadOnlyDictionary<Guid, PersonBusy> Busy, BuildingClock Clock);
