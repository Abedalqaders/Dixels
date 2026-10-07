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
///
/// A preview is read a page at a time, so each answer is the full count plus only the first
/// <c>first</c> reservations, soonest first (all of them when a save asks: <see cref="int.MaxValue"/>).
/// </summary>
public interface IReservationImpactProvider
{
    /// <summary>What it holds in these rooms that the proposed rules (and an added closure) would no longer allow.</summary>
    Task<ReservationImpactPart> FindNoLongerFittingAsync(RoomRulesChange change, int first);

    /// <summary>Everything upcoming it holds in <paramref name="scope"/>: a delete takes all of it, for <paramref name="reason"/>.</summary>
    Task<ReservationImpactPart> FindUpcomingAsync(Building building, RoomScope scope, string reason, int first);

    /// <summary>
    /// The same as <see cref="FindNoLongerFittingAsync"/>, as a save carries it on: which, and
    /// the first rule each breaks — all of them, and no names (nobody reads them).
    /// </summary>
    Task<List<AffectedReservation>> FindAffectedAsync(RoomRulesChange change);

    /// <summary>How many upcoming reservations it holds in <paramref name="scope"/> — counted, nothing described.</summary>
    Task<int> CountUpcomingAsync(RoomScope scope);

    /// <summary>What a person holds in a building they're leaving (moved or unassigned), for <paramref name="reason"/>.</summary>
    Task<ReservationImpactPart> FindForPersonLeavingAsync(Guid userId, Building building, string reason, int first);
}

/// <summary>One module's answer: how many in all, and the first of them, soonest first.</summary>
public sealed record ReservationImpactPart(int Count, IReadOnlyList<RankedReservation> Items);

/// <summary>An affected reservation with its start in UTC, so answers from several modules merge in time order.</summary>
public sealed record RankedReservation(DateTimeOffset StartsAt, AffectedReservationDto Reservation);

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
