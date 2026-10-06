using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dixels.SpaceManagement;

namespace Dixels.Reservations;

/// <summary>
/// Implemented by every module that holds something in rooms for people (Bookings today;
/// parking or visitors later), so an admin's change can be previewed — "this would affect N
/// reservations" — without space management or user management knowing those modules exist.
/// The questions are asked in space-management terms; each module answers for what it holds.
/// Questions only: nothing here changes anything (what happens on save is announced as events).
/// </summary>
public interface IReservationImpactProvider
{
    /// <summary>What it holds in these rooms that the proposed rules (and an added closure) would no longer allow.</summary>
    Task<List<AffectedReservationDto>> FindNoLongerFittingAsync(RoomRulesChange change);

    /// <summary>Everything upcoming it holds in these rooms: a delete takes all of it, for <paramref name="reason"/>.</summary>
    Task<List<AffectedReservationDto>> FindUpcomingAsync(Building building, IReadOnlyList<(Space Space, Floor Floor)> rooms, string reason);

    /// <summary>How many upcoming reservations it holds in these rooms — counted, nothing described.</summary>
    Task<int> CountUpcomingAsync(IReadOnlyCollection<Guid> spaceIds);

    /// <summary>What a person holds in a building they're leaving (moved or unassigned), for <paramref name="reason"/>.</summary>
    Task<List<AffectedReservationDto>> FindForPersonLeavingAsync(Guid userId, Building building, string reason);
}

/// <summary>
/// A proposed change to the rules of some rooms, worked out on unsaved copies — nothing is
/// saved while it's previewed.
/// </summary>
/// <param name="Building">The building the rooms are in (its clock describes the times).</param>
/// <param name="Rooms">Every room the change reaches, with its floor.</param>
/// <param name="ProposedRules">Each room's rules as they would be after the change.</param>
/// <param name="AddedClosure">A new closure being added, unsaved; null for a rule change.</param>
public record RoomRulesChange(
    Building Building,
    IReadOnlyList<(Space Space, Floor Floor)> Rooms,
    Func<Space, Floor, ResolvedConstraints> ProposedRules,
    AvailabilityOverride? AddedClosure = null);
