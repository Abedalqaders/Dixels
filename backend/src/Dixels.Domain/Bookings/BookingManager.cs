using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Json;

namespace Dixels.Bookings;

/// <summary>
/// The one path every booking goes through, for both the dry-run preview and the real
/// create: load the hierarchy → check access → convert local time to UTC → resolve the rules
/// → load closures → validate. Preview and create sharing this means the UI can never be
/// told "free" for a slot the server would then reject.
/// </summary>
public partial class BookingManager : DomainService
{
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<AvailabilityOverride, Guid> _overrideRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<BookingSeries, Guid> _seriesRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly BookingPolicyValidator _validator;
    private readonly BookingAccessChecker _accessChecker;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly BookingOptions _options;
    private readonly LocalizedNameReader _nameReader;
    private readonly IStringLocalizer<DixelsResource> _localizer;
    private readonly ILocalEventBus _localEventBus;
    private readonly BookingInviteeResolver _inviteeResolver;

    public BookingManager(
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        IRepository<AvailabilityOverride, Guid> overrideRepository,
        IBookingRepository bookingRepository,
        IRepository<BookingSeries, Guid> seriesRepository,
        ConstraintResolver constraintResolver,
        BookingPolicyValidator validator,
        BookingAccessChecker accessChecker,
        IJsonSerializer jsonSerializer,
        IOptions<BookingOptions> options,
        LocalizedNameReader nameReader,
        IStringLocalizer<DixelsResource> localizer,
        ILocalEventBus localEventBus,
        BookingInviteeResolver inviteeResolver)
    {
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _overrideRepository = overrideRepository;
        _bookingRepository = bookingRepository;
        _seriesRepository = seriesRepository;
        _constraintResolver = constraintResolver;
        _validator = validator;
        _accessChecker = accessChecker;
        _jsonSerializer = jsonSerializer;
        _options = options.Value;
        _nameReader = nameReader;
        _localizer = localizer;
        _localEventBus = localEventBus;
        _inviteeResolver = inviteeResolver;
    }

    /// <summary>
    /// Checks a request without reserving anything. "Available now" is not a reservation —
    /// the slot can still be taken before the real create commits. The guest list comes back
    /// as it would be saved (a typed colleague's email already turned into the colleague).
    /// </summary>
    public async Task<BookingEvaluation> EvaluateAsync(
        Guid userId, Guid spaceId, DateTime localStart, DateTime localEnd, int attendees, IReadOnlyCollection<Invitee> invitees)
    {
        var context = await LoadContextAsync(userId, spaceId, localStart, localEnd, attendees);
        var resolved = await _inviteeResolver.ResolveAsync(userId, context.Building.Id, invitees);
        return await ValidateAsync(context, attendees, userId, resolved);
    }

    /// <summary>
    /// Creates a confirmed booking, or throws <see cref="BookingRejectedException"/>. Must run
    /// inside a transaction (ABP's unit of work gives every non-GET request one): the space
    /// lock taken first is held until commit, so a competing request for the same space waits
    /// here and then sees this booking in its overlap check.
    ///
    /// A retry with the same idempotency key returns the booking the first attempt created
    /// (<c>Replayed = true</c>) instead of creating a second one. Only a new booking raises
    /// <see cref="BookingConfirmedEvent"/>. <c>Place</c> is the room, floor and building the
    /// checks loaded (a replay is for the same room), for showing the result without reloading.
    /// </summary>
    public async Task<(Booking Booking, bool Replayed, BookingPlace Place)> CreateAsync(
        Guid userId,
        Guid spaceId,
        DateTime localStart,
        DateTime localEnd,
        int attendees,
        IReadOnlyCollection<Invitee> invitees,
        string? title,
        string idempotencyKey)
    {
        await _bookingRepository.LockSpaceAsync(spaceId);

        var context = await LoadContextAsync(userId, spaceId, localStart, localEnd, attendees);

        // One booking at a time per person: hold the person too, so two of their own
        // requests for different rooms can't both pass the clash check at once.
        if (context.Building.OwnOverlapPolicy == OwnOverlapPolicy.Block)
        {
            await _bookingRepository.LockUserAsync(userId);
        }

        var resolved = await _inviteeResolver.ResolveAsync(userId, context.Building.Id, invitees);

        var existing = await _bookingRepository.FindByIdempotencyKeyAsync(userId, idempotencyKey);
        if (existing is not null)
        {
            // A replay must match what the key was first used for. A booking that has since been
            // cancelled is not "already created" either: the caller needs a fresh key (and a
            // fresh check) rather than a cancelled record handed back as a success.
            if (existing.Status == BookingStatus.Cancelled || !existing.MatchesRequest(spaceId, context.StartUtc, context.EndUtc, attendees, resolved))
            {
                throw new BusinessException(DixelsDomainErrorCodes.BookingIdempotencyKeyReused);
            }

            return (existing, true, context.Place);
        }

        var evaluation = await ValidateAsync(context, attendees, userId, resolved);
        if (!evaluation.IsValid)
        {
            throw new BookingRejectedException(evaluation.Violations);
        }

        var booking = new Booking(
            GuidGenerator.Create(),
            spaceId,
            userId,
            evaluation.StartUtc,
            evaluation.EndUtc,
            attendees,
            title,
            _jsonSerializer.Serialize(BookingRuleSnapshot.From(evaluation.Rules)),
            idempotencyKey);
        booking.SetInvitees(resolved, GuidGenerator);

        // Saved with its guests, so listeners of the event below see them on the booking.
        booking = await _bookingRepository.InsertConfirmedAsync(booking);
        await _localEventBus.PublishAsync(new BookingConfirmedEvent(booking));
        return (booking, false, context.Place);
    }

    /// <summary>
    /// The owner cancelling their own booking, which frees the slot straight away. Only
    /// before it starts: once a booking is under way (or over) it's part of the record.
    /// Raises nothing: the scoped <c>CancelOwnAsync</c> that calls it announces the cancel.
    /// </summary>
    private async Task<Booking> CancelOneAsync(Guid userId, Guid bookingId, string? reason) =>
        await CancelOneAsync(userId, await _bookingRepository.GetAsync(bookingId), reason);

    /// <summary>The same, for a booking the caller has already loaded.</summary>
    private async Task<Booking> CancelOneAsync(Guid userId, Booking booking, string? reason)
    {
        if (booking.UserId != userId)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingNotYours);
        }

        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
        if (booking.Status == BookingStatus.Confirmed && booking.StartsAt <= now)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingAlreadyStarted);
        }

        booking.Cancel(userId, now, reason, byAdmin: false);
        return await _bookingRepository.UpdateAsync(booking, autoSave: true);
    }

    // A room that's unavailable only because of *when* (booked, closed for maintenance,
    // outside hours) can be offered a later time; one that's too small or too far ahead can't.
    private static readonly HashSet<string> TimeOnlyViolations = new()
    {
        DixelsDomainErrorCodes.BookingOverlap,
        DixelsDomainErrorCodes.BookingSpaceClosed,
        DixelsDomainErrorCodes.BookingOutsideHours,
    };

    /// <summary>
    /// "Which spaces in my building can I book for this window?" — every space (optionally
    /// narrowed by floor/type) checked by the same validator a real booking runs, plus that
    /// space's day around the window for the timeline. Bookings and closures for the whole
    /// building's day are loaded in one query each, not one per space. The window must sit
    /// within one building-local day.
    /// </summary>
    public async Task<AvailabilitySearch> SearchAsync(
        Guid userId,
        DateTime localStart,
        DateTime localEnd,
        int attendees,
        Guid? floorId = null,
        Guid? spaceTypeId = null)
    {
        if (attendees < 1)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingAttendeesMustBePositive);
        }

        var buildingId = await _accessChecker.FindBookableBuildingIdAsync(userId);
        var building = buildingId is null ? null : await _buildingRepository.FindAsync(buildingId.Value);
        if (building is null)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingNotAssignedToBuilding);
        }

        var clock = new BuildingClock(building.Timezone);
        var startUtc = clock.ToUtc(localStart);
        var endUtc = clock.ToUtc(localEnd);
        var date = DateOnly.FromDateTime(localStart);
        var day = new TimeRange(clock.StartOfLocalDay(date), clock.StartOfLocalDay(date.AddDays(1)));

        if (endUtc <= startUtc || endUtc > day.End)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingInvalidTimeRange);
        }

        // With details: their names, which the results show.
        var floors = await _floorRepository.GetListAsync(f =>
            f.BuildingId == building.Id && (floorId == null || f.Id == floorId), includeDetails: true);
        var floorIds = floors.Select(f => f.Id).ToList();

        var spaces = await _spaceRepository.GetListAsync(s =>
            floorIds.Contains(s.FloorId) && (spaceTypeId == null || s.SpaceTypeId == spaceTypeId), includeDetails: true);
        var spaceIds = spaces.Select(s => s.Id).ToList();

        var dayOverrides = await _overrideRepository.GetListAsync(o =>
            ((o.Scope == OverrideScope.Building && o.ScopeId == building.Id)
             || (o.Scope == OverrideScope.Floor && floorIds.Contains(o.ScopeId))
             || (o.Scope == OverrideScope.Space && spaceIds.Contains(o.ScopeId)))
            && o.StartsAt < day.End
            && o.EndsAt > day.Start);

        var bookingsBySpace = (await _bookingRepository.GetConfirmedOverlappingAsync(spaceIds, day.Start, day.End))
            .ToLookup(b => b.SpaceId);

        var floorById = floors.ToDictionary(f => f.Id);
        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
        var request = new BookingRequest(startUtc, endUtc, attendees);

        // Checked once for the whole search: the person's clash doesn't depend on the room.
        var ownClash = await FindOwnClashAsync(userId, building, clock, startUtc, endUtc, exceptSpaceId: null);

        var results = spaces.Select(space =>
        {
            var floor = floorById[space.FloorId];
            var rules = _constraintResolver.Resolve(building, floor, space);

            // Closures union across levels: the space's own, its floor's, and the building's.
            var overrides = dayOverrides
                .Where(o => (o.Scope == OverrideScope.Space && o.ScopeId == space.Id)
                            || (o.Scope == OverrideScope.Floor && o.ScopeId == floor.Id)
                            || o.Scope == OverrideScope.Building)
                .Select(OverrideWindow.From)
                .ToList();

            var busy = bookingsBySpace[space.Id]
                .Select(b => new BusyRange(new TimeRange(b.StartsAt, b.EndsAt), b.UserId == userId))
                .ToList();

            var violations = _validator.Validate(
                rules,
                clock,
                request,
                overrides.Where(o => o.Range.Overlaps(startUtc, endUtc)).ToList(),
                busy.Any(b => b.Range.Overlaps(startUtc, endUtc)),
                now,
                _options.SlotMinutes);

            // Under Block, the person's other booking rules out every room — except the one
            // it's in, which already says "already booked" on its own.
            if (ownClash is { Blocks: true } && ownClash.Value.SpaceId != space.Id)
            {
                violations = violations.Append(ownClash.Value.Violation).ToList();
            }

            var (open, closed) = OpenAndClosedOn(rules, clock, date, day, overrides);

            var blockers = closed.Concat(busy.Select(b => b.Range)).ToList();

            DateTimeOffset? freeUntil = null;
            DateTimeOffset? nextFreeStart = null;

            if (violations.Count == 0)
            {
                freeUntil = FreeTime.FreeUntil(open, blockers, startUtc, endUtc);
            }
            else if (violations.All(v => TimeOnlyViolations.Contains(v.Code)))
            {
                nextFreeStart = FreeTime.NextFreeStart(
                    open, blockers, startUtc, endUtc - startUtc, _options.SlotMinutes, day.End);
            }

            return new SpaceAvailability(space, floor, rules, violations, open, closed, busy, freeUntil, nextFreeStart);
        }).ToList();

        var warnings = ownClash is { Blocks: false } ? new[] { ownClash.Value.Violation } : Array.Empty<BookingViolation>();
        return new AvailabilitySearch(building, clock, startUtc, endUtc, day, results, warnings);
    }

    /// <summary>
    /// One building-local day of a space: when it's open (its days and hours plus special
    /// openings) and when an admin closed it, both cut to that day. Closures aren't taken out
    /// of the open times — they're shown, and refused, as their own thing.
    /// </summary>
    private static (List<TimeRange> Open, List<TimeRange> Closed) OpenAndClosedOn(
        ResolvedConstraints rules, BuildingClock clock, DateOnly date, TimeRange day, IEnumerable<OverrideWindow> overrides)
    {
        var open = OpenIntervals.Compute(
                rules.Days.Value,
                rules.Hours.Value,
                clock,
                date,
                date,
                overrides.Where(o => o.Effect == OverrideEffect.Open).Select(o => o.Range))
            .Select(r => r.ClipTo(day))
            .OfType<TimeRange>()
            .ToList();

        var closed = overrides
            .Where(o => o.Effect == OverrideEffect.Closed)
            .Select(o => o.Range.ClipTo(day))
            .OfType<TimeRange>()
            .ToList();

        return (open, closed);
    }

    private async Task<BookingContext> LoadContextAsync(Guid userId, Guid spaceId, DateTime localStart, DateTime localEnd, int attendees)
    {
        if (attendees < 1)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingAttendeesMustBePositive);
        }

        // GetAsync respects the soft-delete filter, so a deleted space (or one under a
        // deleted floor/building) is a plain 404 rather than something bookable.
        var space = await _spaceRepository.GetAsync(spaceId);
        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);

        await _accessChecker.EnsureCanBookAsync(userId, building.Id);

        var clock = new BuildingClock(building.Timezone);
        var startUtc = clock.ToUtc(localStart);
        var endUtc = clock.ToUtc(localEnd);

        if (endUtc <= startUtc)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingInvalidTimeRange);
        }

        return new BookingContext(space, floor, building, clock, startUtc, endUtc);
    }

    private async Task<BookingEvaluation> ValidateAsync(BookingContext context, int attendees, Guid userId, IReadOnlyList<Invitee> invitees)
    {
        var rules = _constraintResolver.Resolve(context.Building, context.Floor, context.Space);
        var overrides = await LoadOverlappingOverridesAsync(context);
        var overlaps = await _bookingRepository.AnyConfirmedOverlapAsync(context.Space.Id, context.StartUtc, context.EndUtc);

        var violations = _validator.Validate(
            rules,
            context.LocalClock,
            new BookingRequest(context.StartUtc, context.EndUtc, attendees, invitees.Count),
            overrides,
            overlaps,
            new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero),
            _options.SlotMinutes);

        // The person's own other booking at that time (a different room — the same room is
        // already "already booked"): a rule under Block, a heads-up under Warn.
        var ownClash = await FindOwnClashAsync(
            userId, context.Building, context.LocalClock, context.StartUtc, context.EndUtc, exceptSpaceId: context.Space.Id);
        var warnings = new List<BookingViolation>();
        if (ownClash is { } clash)
        {
            if (clash.Blocks)
            {
                violations = violations.Append(clash.Violation).ToList();
            }
            else
            {
                warnings.Add(clash.Violation);
            }
        }

        return new BookingEvaluation(
            context.Space, context.Floor, context.Building, context.LocalClock, rules, context.StartUtc, context.EndUtc, violations, warnings, invitees);
    }

    private readonly record struct OwnClash(BookingViolation Violation, Guid SpaceId, bool Blocks);

    /// <summary>
    /// The person's earliest other confirmed booking overlapping <c>[start, end)</c> in a
    /// building that cares (Warn or Block), as the message to show — or null when there's
    /// none, or the building allows it silently.
    /// </summary>
    private async Task<OwnClash?> FindOwnClashAsync(
        Guid userId, Building building, BuildingClock clock, DateTimeOffset start, DateTimeOffset end, Guid? exceptSpaceId)
    {
        if (building.OwnOverlapPolicy == OwnOverlapPolicy.Allow)
        {
            return null;
        }

        var clash = (await _bookingRepository.GetConfirmedForUserAsync(userId, start, end))
            .FirstOrDefault(b => b.SpaceId != exceptSpaceId);
        if (clash is null)
        {
            return null;
        }

        // FindAsync, not GetAsync: the other room may have been deleted since — still a clash.
        var spaceName = await RoomNameAsync(clash.SpaceId);
        var blocks = building.OwnOverlapPolicy == OwnOverlapPolicy.Block;

        var violation = OwnClashViolation(clash, spaceName, clock, blocks);

        return new OwnClash(violation, clash.SpaceId, blocks);
    }

    /// <summary>A room's name in the reader's language — "another room" if it has since been deleted.</summary>
    private async Task<string> RoomNameAsync(Guid spaceId)
    {
        var space = await _spaceRepository.FindAsync(spaceId);
        return space is null ? _localizer["Dixels:Bookings:AnotherRoom"].Value : await _nameReader.ShownAsync(space);
    }

    /// <summary>Several rooms' names at once, in one query — "another room" for any deleted since.</summary>
    private async Task<Dictionary<Guid, string>> RoomNamesAsync(IReadOnlyCollection<Guid> spaceIds)
    {
        if (spaceIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var spaces = await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id), includeDetails: true);
        var names = await _nameReader.ShownAsync<Space, SpaceTranslation>(spaces);
        return spaceIds.ToDictionary(id => id, id => names.GetValueOrDefault(id) ?? _localizer["Dixels:Bookings:AnotherRoom"].Value);
    }

    // Closures union across levels, so overrides on the space, its floor and its building
    // all apply. Only ones overlapping the request matter (for both closures and special
    // openings — an opening that doesn't touch the request can't help cover it).
    private async Task<IReadOnlyList<OverrideWindow>> LoadOverlappingOverridesAsync(BookingContext context)
    {
        var spaceId = context.Space.Id;
        var floorId = context.Floor.Id;
        var buildingId = context.Building.Id;
        var start = context.StartUtc;
        var end = context.EndUtc;

        var overrides = await _overrideRepository.GetListAsync(o =>
            ((o.Scope == OverrideScope.Space && o.ScopeId == spaceId)
             || (o.Scope == OverrideScope.Floor && o.ScopeId == floorId)
             || (o.Scope == OverrideScope.Building && o.ScopeId == buildingId))
            && o.StartsAt < end
            && o.EndsAt > start);

        return overrides.Select(OverrideWindow.From).ToList();
    }

    private sealed record BookingContext(
        Space Space,
        Floor Floor,
        Building Building,
        BuildingClock LocalClock,
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc)
    {
        public BookingPlace Place => new(Space, Floor, Building);
    }
}
