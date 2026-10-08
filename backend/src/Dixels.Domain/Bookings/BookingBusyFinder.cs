using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

namespace Dixels.Bookings;

/// <summary>
/// A time someone is taken: their own confirmed booking or a meeting they accepted (busy), or
/// a meeting they said Maybe to (<see cref="IsTentative"/>: maybe busy, like Outlook's
/// tentative). An invite still pending or declined doesn't count.
/// </summary>
public readonly record struct BusySlot(Guid UserId, TimeRange Range, bool IsAcceptedInvite, bool IsTentative = false);

/// <summary>A stretch of busy time; <see cref="Tentative"/>: only maybe busy then (a Maybe answer).</summary>
public readonly record struct BusyTime(TimeRange Range, bool Tentative);

/// <summary>
/// One person's busy times within a booking's time (or a series' dates): each cut to the
/// slot it overlaps and merged, earliest first — times only, never with what. Firm busy wins:
/// a maybe-busy stretch only shows where they aren't busy anyway. <see cref="Dates"/>: on how
/// many of the slots they're busy; <see cref="MaybeDates"/>: on how many more only maybe.
/// </summary>
public sealed record PersonBusy(int Dates, IReadOnlyList<BusyTime> Times, int MaybeDates = 0)
{
    /// <summary>Only maybe busy: on no date firmly (the amber ring rather than the red one).</summary>
    public bool OnlyMaybe => Dates == 0 && MaybeDates > 0;
}

/// <summary>
/// Who among some colleagues is busy (or maybe busy) at some times — for the guest picker's
/// presence ring and pill.
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
            var (dates, maybeDates) = (0, 0);
            var times = new List<BusyTime>();
            foreach (var slot in slots.OrderBy(s => s.Start))
            {
                var firm = Merge(person.Where(b => !b.IsTentative).Select(b => b.Range.ClipTo(slot)).OfType<TimeRange>());
                var maybe = Merge(person.Where(b => b.IsTentative).Select(b => b.Range.ClipTo(slot)).OfType<TimeRange>())
                    .SelectMany(r => Subtract(r, firm))
                    .ToList();
                if (firm.Count > 0)
                {
                    dates++;
                }
                else if (maybe.Count > 0)
                {
                    maybeDates++;
                }

                times.AddRange(firm.Select(r => new BusyTime(r, false))
                    .Concat(maybe.Select(r => new BusyTime(r, true)))
                    .OrderBy(t => t.Range.Start));
            }

            if (dates + maybeDates > 0)
            {
                result[person.Key] = new PersonBusy(dates, times, maybeDates);
            }
        }

        return result;
    }

    /// <summary>What's left of <paramref name="range"/> outside the (sorted, merged) <paramref name="taken"/> ranges.</summary>
    private static IEnumerable<TimeRange> Subtract(TimeRange range, IReadOnlyList<TimeRange> taken)
    {
        var start = range.Start;
        foreach (var t in taken.Where(t => t.End > range.Start && t.Start < range.End))
        {
            if (t.Start > start)
            {
                yield return new TimeRange(start, t.Start);
            }

            if (t.End > start)
            {
                start = t.End;
            }
        }

        if (start < range.End)
        {
            yield return new TimeRange(start, range.End);
        }
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
