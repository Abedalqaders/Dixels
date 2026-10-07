using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Volo.Abp.DependencyInjection;

namespace Dixels.Bookings;

/// <summary>
/// Bookings' answer to "what would this change do to what you hold?" (see
/// <see cref="IReservationImpactProvider"/>): the upcoming bookings an admin's change would
/// cancel, as the admin reads them before confirming — counted in full, but only the first
/// <c>first</c> described (each with its person's and room's names).
/// </summary>
[ExposeServices(typeof(IReservationImpactProvider))]
public class BookingImpactProvider : IReservationImpactProvider, ITransientDependency
{
    private readonly BookingImpactService _bookingImpact;

    public BookingImpactProvider(BookingImpactService bookingImpact)
    {
        _bookingImpact = bookingImpact;
    }

    public async Task<ReservationImpactPart> FindNoLongerFittingAsync(RoomRulesChange change, int first)
    {
        // Every booking has to be checked to know how many fail; only the shown ones are described.
        var closure = change.AddedClosure is null ? null : OverrideWindow.From(change.AddedClosure);
        var misfits = await _bookingImpact.Checker.FindMisfitsAsync(change.Building, change.Rooms, change.ProposedRules, closure);
        return new ReservationImpactPart(misfits.Count, await _bookingImpact.DescribeAsync(change.Building, misfits.Take(first).ToList()));
    }

    public async Task<ReservationImpactPart> FindUpcomingAsync(Building building, RoomScope scope, string reason, int first)
    {
        // A delete takes every one, so they're counted and paged in SQL. Fewer than asked for
        // is all there is: only a full page needs the COUNT.
        var shown = await _bookingImpact.Checker.FindUpcomingAsync(scope, first);
        var count = shown.Count < first ? shown.Count : await _bookingImpact.Checker.CountUpcomingAsync(scope);
        return new ReservationImpactPart(count, await _bookingImpact.DescribeAsync(building, Unbroken(shown), reason));
    }

    public async Task<List<AffectedReservation>> FindAffectedAsync(RoomRulesChange change)
    {
        var closure = change.AddedClosure is null ? null : OverrideWindow.From(change.AddedClosure);
        var misfits = await _bookingImpact.Checker.FindMisfitsAsync(change.Building, change.Rooms, change.ProposedRules, closure);
        return _bookingImpact.ToAffected(misfits);
    }

    public Task<int> CountUpcomingAsync(RoomScope scope) =>
        _bookingImpact.Checker.CountUpcomingAsync(scope);

    public async Task<ReservationImpactPart> FindForPersonLeavingAsync(Guid userId, Building building, string reason, int first)
    {
        // A move releases every one, so the same as a delete.
        var shown = await _bookingImpact.Checker.FindUpcomingForUserAsync(userId, building.Id, first);
        var count = shown.Count < first ? shown.Count : await _bookingImpact.Checker.CountUpcomingForUserAsync(userId, building.Id);
        return new ReservationImpactPart(count, await _bookingImpact.DescribeAsync(building, Unbroken(shown), reason));
    }

    private static List<BookingMisfit> Unbroken(IEnumerable<UpcomingBooking> bookings) =>
        bookings.Select(b => new BookingMisfit(b, Array.Empty<BookingViolation>())).ToList();
}
