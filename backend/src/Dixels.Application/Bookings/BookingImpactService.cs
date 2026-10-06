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
    /// A person's upcoming bookings as impacts — all of them, or only those in
    /// <paramref name="buildingId"/> (moving them elsewhere). Also returns that building, for
    /// describing them on its clock.
    /// </summary>
    public async Task<(Building? Building, IReadOnlyList<BookingImpact> Impacts)> UpcomingForUserAsync(Guid userId, Guid? buildingId = null)
    {
        var bookings = await _checker.FindUpcomingForUserAsync(userId);
        if (bookings.Count == 0)
        {
            return (buildingId is null ? null : await _buildingRepository.FindAsync(buildingId.Value), Array.Empty<BookingImpact>());
        }

        // Deleted rooms included: they're still where the booking is.
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var spaceIds = bookings.Select(b => b.SpaceId).Distinct().ToList();
            var spaces = (await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id);
            var floorIds = spaces.Values.Select(s => s.FloorId).Distinct().ToList();
            var floors = (await _floorRepository.GetListAsync(f => floorIds.Contains(f.Id))).ToDictionary(f => f.Id);

            var impacts = bookings
                .Select(b => (Booking: b, Space: spaces[b.SpaceId], Floor: floors[spaces[b.SpaceId].FloorId]))
                .Where(x => buildingId is null || x.Floor.BuildingId == buildingId)
                .Select(x => new BookingImpact(x.Booking, x.Space, x.Floor, Array.Empty<BookingViolation>()))
                .ToList();

            var building = buildingId is not null
                ? await _buildingRepository.FindAsync(buildingId.Value)
                : impacts.Count > 0 ? await _buildingRepository.FindAsync(impacts[0].Floor.BuildingId) : null;
            return (building, impacts);
        }
    }

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

        var (_, upcoming) = await UpcomingForUserAsync(userId, fromBuildingId);
        await CancelAllAsync(upcoming, adminId, Text("Dixels:Bookings:CancelReason:MovedBuilding"));
    }

    public BookingImpactChecker Checker => _checker;

    /// <summary>The bookings as the admin reads them; <paramref name="fixedReason"/> replaces the per-rule reasons (a delete).</summary>
    public async Task<List<AffectedReservationDto>> DescribeAsync(Building building, IReadOnlyList<BookingImpact> impacts, string? fixedReason = null)
    {
        var clock = new BuildingClock(building.Timezone);
        var names = await RoomNamesAsync(impacts);
        var userIds = impacts.Select(i => i.Booking.UserId).Distinct().ToList();
        var users = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _userRepository.GetListByIdsAsync(userIds)).ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.Name) ? u.UserName : $"{u.Name} {u.Surname}".Trim());

        return impacts.Select(i => new AffectedReservationDto
            {
                Kind = ReservationKinds.Booking,
                Id = i.Booking.Id,
                Title = i.Booking.Title,
                HeldBy = users.GetValueOrDefault(i.Booking.UserId, "Someone"),
                PlaceName = names[i.Space.Id],
                PlaceDetail = names[i.Floor.Id],
                LocalStart = clock.ToLocal(i.Booking.StartsAt),
                LocalEnd = clock.ToLocal(i.Booking.EndsAt),
                Reasons = fixedReason is not null
                    ? new List<string> { fixedReason }
                    : i.Violations.Select(v => _violationLocalizer.ToDto(v).ShortMessage).Distinct().ToList(),
            }).ToList();
    }

    /// <summary>
    /// The rooms' and floors' names in the reader's language, by id. Read afresh with their
    /// names: the impacts come from many places, not all of which load them. Deleted ones
    /// included — they're still where the booking is.
    /// </summary>
    private async Task<Dictionary<Guid, string>> RoomNamesAsync(IReadOnlyList<BookingImpact> impacts)
    {
        if (impacts.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var spaceIds = impacts.Select(i => i.Space.Id).Distinct().ToList();
        var floorIds = impacts.Select(i => i.Floor.Id).Distinct().ToList();
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var spaces = await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id), includeDetails: true);
            var floors = await _floorRepository.GetListAsync(f => floorIds.Contains(f.Id), includeDetails: true);
            return (await _nameReader.ShownAsync<Space, SpaceTranslation>(spaces))
                .Concat(await _nameReader.ShownAsync<Floor, FloorTranslation>(floors))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }

    /// <summary>Cancels bookings a rule change broke: "Rules changed: Open 09:00–17:00 only".</summary>
    public Task CancelForRuleChangeAsync(IReadOnlyList<BookingImpact> impacts, Guid adminId)
    {
        var reasons = impacts.ToDictionary(
            i => i.Booking.Id,
            i => _localizer["Dixels:Bookings:CancelReason:RulesChanged", _violationLocalizer.ToDto(i.Violations[0]).ShortMessage].Value);
        return _checker.CancelAsAdminAsync(impacts.Select(i => i.Booking).ToList(), adminId, b => reasons[b.Id]);
    }

    /// <summary>Cancels bookings with one reason for all (a closure, a removed room).</summary>
    public Task CancelAllAsync(IReadOnlyList<BookingImpact> impacts, Guid adminId, string reason) =>
        _checker.CancelAsAdminAsync(impacts.Select(i => i.Booking).ToList(), adminId, _ => reason);

    public string Text(string key, params object[] args) => _localizer[key, args].Value;

    /// <summary>Every upcoming booking on these rooms, as impacts with no rule broken — for a delete.</summary>
    public async Task<IReadOnlyList<BookingImpact>> UpcomingAsync(IReadOnlyList<(Space Space, Floor Floor)> rooms)
    {
        var byId = rooms.ToDictionary(r => r.Space.Id);
        var bookings = await _checker.FindUpcomingAsync(byId.Keys.ToList());
        return bookings
            .Select(b => new BookingImpact(b, byId[b.SpaceId].Space, byId[b.SpaceId].Floor, Array.Empty<BookingViolation>()))
            .ToList();
    }
}
