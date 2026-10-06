using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Volo.Abp.DependencyInjection;

namespace Dixels.Bookings;

/// <summary>
/// Bookings' answer to "what would this change do to what you hold?" (see
/// <see cref="IReservationImpactProvider"/>): the upcoming bookings an admin's change would
/// cancel, as the admin reads them before confirming.
/// </summary>
[ExposeServices(typeof(IReservationImpactProvider))]
public class BookingImpactProvider : IReservationImpactProvider, ITransientDependency
{
    private readonly BookingImpactService _bookingImpact;

    public BookingImpactProvider(BookingImpactService bookingImpact)
    {
        _bookingImpact = bookingImpact;
    }

    public async Task<List<AffectedReservationDto>> FindNoLongerFittingAsync(RoomRulesChange change)
    {
        var closure = change.AddedClosure is null ? null : OverrideWindow.From(change.AddedClosure);
        var broken = await _bookingImpact.Checker.FindNoLongerFittingAsync(change.Building, change.Rooms, change.ProposedRules, closure);
        return await _bookingImpact.DescribeAsync(change.Building, broken);
    }

    public async Task<List<AffectedReservationDto>> FindUpcomingAsync(Building building, IReadOnlyList<(Space Space, Floor Floor)> rooms, string reason) =>
        await _bookingImpact.DescribeAsync(building, await _bookingImpact.UpcomingAsync(rooms), reason);

    public Task<int> CountUpcomingAsync(IReadOnlyCollection<Guid> spaceIds) =>
        _bookingImpact.Checker.CountUpcomingAsync(spaceIds);

    public async Task<List<AffectedReservationDto>> FindForPersonLeavingAsync(Guid userId, Building building, string reason)
    {
        var (_, upcoming) = await _bookingImpact.UpcomingForUserAsync(userId, building.Id);
        return await _bookingImpact.DescribeAsync(building, upcoming, reason);
    }
}
