using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Localization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Dixels.Bookings;

/// <summary>
/// The application side of <see cref="BookingImpactChecker"/>, shared by every admin action
/// that can leave upcoming bookings behind (a constraint save at any level, a new closure,
/// a delete): turns the affected bookings into what the admin reads before confirming, and
/// cancels them with a reason the employee will see on their calendar.
/// </summary>
public class BookingImpactService : ITransientDependency
{
    private readonly BookingImpactChecker _checker;
    private readonly IIdentityUserRepository _userRepository;
    private readonly BookingViolationLocalizer _violationLocalizer;
    private readonly IStringLocalizer<DixelsResource> _localizer;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IDataFilter _dataFilter;
    private readonly LocalizedNameReader _nameReader;

    public BookingImpactService(
        BookingImpactChecker checker,
        IIdentityUserRepository userRepository,
        BookingViolationLocalizer violationLocalizer,
        IStringLocalizer<DixelsResource> localizer,
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        IDataFilter dataFilter,
        LocalizedNameReader nameReader)
    {
        _checker = checker;
        _userRepository = userRepository;
        _violationLocalizer = violationLocalizer;
        _localizer = localizer;
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _dataFilter = dataFilter;
        _nameReader = nameReader;
    }

    /// <summary>
    /// A person's upcoming bookings, for cancelling them — all of them, or only those in
    /// <paramref name="buildingId"/> (moving them elsewhere; picked out in the same query).
    /// </summary>
    public async Task<List<Booking>> UpcomingForUserAsync(Guid userId, Guid? buildingId = null) =>
        buildingId is { } building
            ? await _checker.FindUpcomingForUserInBuildingAsync(userId, building)
            : await _checker.FindUpcomingForUserAsync(userId);

    /// <summary>
    /// Someone leaves <paramref name="fromBuildingId"/> (moved or unassigned): they can only
    /// book in one building, so their upcoming bookings there are cancelled.
    /// </summary>
    public async Task CancelOnMoveAsync(Guid userId, Guid? fromBuildingId, Guid? toBuildingId, Guid adminId)
    {
        if (fromBuildingId is null || fromBuildingId == toBuildingId)
        {
            return;
        }

        await CancelAllAsync(await UpcomingForUserAsync(userId, fromBuildingId), adminId, Text("Dixels:Bookings:CancelReason:MovedBuilding"));
    }

    public BookingImpactChecker Checker => _checker;

    /// <summary>
    /// The bookings as the admin reads them, each with its UTC start for ordering;
    /// <paramref name="fixedReason"/> replaces the per-rule reasons (a delete). Pass only the
    /// ones that will be shown: each name is looked up once for the lot.
    /// </summary>
    public async Task<List<RankedReservation>> DescribeAsync(Building building, IReadOnlyList<BookingMisfit> misfits, string? fixedReason = null)
    {
        if (misfits.Count == 0)
        {
            return new List<RankedReservation>();
        }

        var clock = new BuildingClock(building.Timezone);
        var places = await PlacesAsync(misfits.Select(m => m.Booking.SpaceId).Distinct().ToList());
        var userIds = misfits.Select(m => m.Booking.UserId).Distinct().ToList();
        var users = (await _userRepository.GetListByIdsAsync(userIds)).ToDictionary(
            u => u.Id,
            u => string.IsNullOrWhiteSpace(u.Name) ? u.UserName : $"{u.Name} {u.Surname}".Trim());

        return misfits.Select(m => new RankedReservation(m.Booking.StartsAt, new AffectedReservationDto
            {
                Kind = ReservationKinds.Booking,
                Id = m.Booking.Id,
                Title = m.Booking.Title,
                HeldBy = users.GetValueOrDefault(m.Booking.UserId, "Someone"),
                PlaceName = places[m.Booking.SpaceId].Room,
                PlaceDetail = places[m.Booking.SpaceId].Floor,
                LocalStart = clock.ToLocal(m.Booking.StartsAt),
                LocalEnd = clock.ToLocal(m.Booking.EndsAt),
                Reasons = fixedReason is not null
                    ? new List<string> { fixedReason }
                    : m.Violations.Select(v => _violationLocalizer.ToDto(v).ShortMessage).Distinct().ToList(),
            })).ToList();
    }

    /// <summary>
    /// As a save carries them on: each booking with the first rule it breaks, in the words
    /// <see cref="DescribeAsync"/> shows it (its first reason) — nothing else is looked up.
    /// </summary>
    public List<AffectedReservation> ToAffected(IReadOnlyList<BookingMisfit> misfits) =>
        misfits
            .Select(m => new AffectedReservation(
                ReservationKinds.Booking,
                m.Booking.Id,
                m.Violations.Count == 0 ? string.Empty : _violationLocalizer.ToDto(m.Violations[0]).ShortMessage))
            .ToList();

    /// <summary>
    /// Each room's name and its floor's, in the reader's language. Deleted ones included —
    /// they're still where the booking is.
    /// </summary>
    private async Task<Dictionary<Guid, (string Room, string Floor)>> PlacesAsync(IReadOnlyCollection<Guid> spaceIds)
    {
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var spaces = await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id), includeDetails: true);
            var floorIds = spaces.Select(s => s.FloorId).Distinct().ToList();
            var floors = await _floorRepository.GetListAsync(f => floorIds.Contains(f.Id), includeDetails: true);
            var roomNames = await _nameReader.ShownAsync<Space, SpaceTranslation>(spaces);
            var floorNames = await _nameReader.ShownAsync<Floor, FloorTranslation>(floors);
            return spaces.ToDictionary(s => s.Id, s => (roomNames[s.Id], floorNames[s.FloorId]));
        }
    }

    /// <summary>Cancels bookings a rule change broke: "Rules changed: Open 09:00–17:00 only".</summary>
    public Task CancelForRuleChangeAsync(IReadOnlyList<BookingImpact> impacts, Guid adminId)
    {
        var reasons = impacts.ToDictionary(
            i => i.Booking.Id,
            i => _localizer["Dixels:Bookings:CancelReason:RulesChanged", _violationLocalizer.ToDto(i.Violations[0]).ShortMessage].Value);
        return CancelInBatchesAsync(reasons.Keys, adminId, id => reasons[id]);
    }

    /// <summary>
    /// The same, for what the save's own preview already found (bookings only, the rest are
    /// other modules'): each with the first rule it breaks, as the preview put it.
    /// </summary>
    public async Task CancelForRuleChangeAsync(IReadOnlyList<AffectedReservation> affected, Guid adminId)
    {
        var reasons = OwnOf(affected).ToDictionary(a => a.Id, a => _localizer["Dixels:Bookings:CancelReason:RulesChanged", a.Reason].Value);
        await CancelInBatchesAsync(reasons.Keys, adminId, id => reasons[id]);
    }

    /// <summary>Cancels bookings with one reason for all (a closure, a removed room).</summary>
    public Task CancelAllAsync(IReadOnlyList<BookingImpact> impacts, Guid adminId, string reason) =>
        CancelInBatchesAsync(impacts.Select(i => i.Booking.Id), adminId, _ => reason);

    /// <summary>The same, for bookings already read (a person's, when they leave or are removed).</summary>
    public Task CancelAllAsync(IReadOnlyList<Booking> bookings, Guid adminId, string reason) =>
        CancelInBatchesAsync(bookings.Select(b => b.Id), adminId, _ => reason);

    /// <summary>The same, for what the save's own preview already found (bookings only).</summary>
    public Task CancelAllAsync(IReadOnlyList<AffectedReservation> affected, Guid adminId, string reason) =>
        CancelInBatchesAsync(OwnOf(affected).Select(a => a.Id), adminId, _ => reason);

    /// <summary>How many bookings one round cancels (one UPDATE and one event per reason), like CancelBookingsInRemovedRoomsJob.</summary>
    public const int CancelBatchSize = 500;

    // A building-wide change can cancel thousands: they go a round at a time, each round one
    // UPDATE per reason (one for a closure; one per broken rule for a rule change), so no
    // statement carries thousands of ids and no booking is loaded and saved one by one. A
    // booking that was cancelled or began since the check is left alone (the UPDATE's own
    // condition). All in the admin's own unit of work, so the save and its cancels still
    // commit (or roll back) together.
    private async Task CancelInBatchesAsync(IEnumerable<Guid> ids, Guid adminId, Func<Guid, string> reason)
    {
        foreach (var batch in ids.Chunk(CancelBatchSize))
        {
            foreach (var sameReason in batch.GroupBy(reason))
            {
                await _checker.CancelUpcomingAsAdminAsync(sameReason.ToList(), adminId, sameReason.Key);
            }
        }
    }

    private static IEnumerable<AffectedReservation> OwnOf(IReadOnlyList<AffectedReservation> affected) =>
        affected.Where(a => a.Kind == ReservationKinds.Booking).DistinctBy(a => a.Id);

    public string Text(string key, params object[] args) => _localizer[key, args].Value;

    /// <summary>
    /// The building and the rooms an event names, each with its floor — what a rules check
    /// needs. Rooms deleted since are left out (nothing can be booked there any more); null
    /// building when it's gone.
    /// </summary>
    public async Task<(Building? Building, List<(Space Space, Floor Floor)> Rooms)> RoomsAsync(Guid buildingId, IReadOnlyCollection<Guid> spaceIds)
    {
        var building = await _buildingRepository.FindAsync(buildingId);
        if (building is null || spaceIds.Count == 0)
        {
            return (building, new List<(Space, Floor)>());
        }

        var spaces = await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id));
        var floorIds = spaces.Select(s => s.FloorId).Distinct().ToList();
        var floors = (await _floorRepository.GetListAsync(f => floorIds.Contains(f.Id))).ToDictionary(f => f.Id);
        return (building, spaces.Where(s => floors.ContainsKey(s.FloorId)).Select(s => (s, floors[s.FloorId])).ToList());
    }
}
