using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Json;

namespace Dixels.Bookings;

/// <summary>
/// The one path every booking goes through, for both the dry-run preview and the real
/// create: load the hierarchy → check access → convert local time to UTC → resolve the rules
/// → load closures → validate. Preview and create sharing this means the UI can never be
/// told "free" for a slot the server would then reject.
/// </summary>
public class BookingManager : DomainService
{
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<AvailabilityOverride, Guid> _overrideRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly BookingPolicyValidator _validator;
    private readonly BookingAccessChecker _accessChecker;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly BookingOptions _options;

    public BookingManager(
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        IRepository<AvailabilityOverride, Guid> overrideRepository,
        IBookingRepository bookingRepository,
        ConstraintResolver constraintResolver,
        BookingPolicyValidator validator,
        BookingAccessChecker accessChecker,
        IJsonSerializer jsonSerializer,
        IOptions<BookingOptions> options)
    {
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _overrideRepository = overrideRepository;
        _bookingRepository = bookingRepository;
        _constraintResolver = constraintResolver;
        _validator = validator;
        _accessChecker = accessChecker;
        _jsonSerializer = jsonSerializer;
        _options = options.Value;
    }

    /// <summary>
    /// Checks a request without reserving anything. "Available now" is not a reservation —
    /// the slot can still be taken before the real create commits.
    /// </summary>
    public async Task<BookingEvaluation> EvaluateAsync(Guid userId, Guid spaceId, DateTime localStart, DateTime localEnd, int attendees)
    {
        var context = await LoadContextAsync(userId, spaceId, localStart, localEnd, attendees);
        return await ValidateAsync(context, attendees);
    }

    /// <summary>
    /// Creates a confirmed booking, or throws <see cref="BookingRejectedException"/>. Must run
    /// inside a transaction (ABP's unit of work gives every non-GET request one): the space
    /// lock taken first is held until commit, so a competing request for the same space waits
    /// here and then sees this booking in its overlap check.
    ///
    /// A retry with the same idempotency key returns the booking the first attempt created
    /// (<c>Replayed = true</c>) instead of creating a second one.
    /// </summary>
    public async Task<(Booking Booking, bool Replayed)> CreateAsync(
        Guid userId,
        Guid spaceId,
        DateTime localStart,
        DateTime localEnd,
        int attendees,
        string? title,
        string idempotencyKey)
    {
        await _bookingRepository.LockSpaceAsync(spaceId);

        var context = await LoadContextAsync(userId, spaceId, localStart, localEnd, attendees);

        var existing = await _bookingRepository.FindByIdempotencyKeyAsync(userId, idempotencyKey);
        if (existing is not null)
        {
            if (!existing.MatchesRequest(spaceId, context.StartUtc, context.EndUtc, attendees))
            {
                throw new BusinessException(DixelsDomainErrorCodes.BookingIdempotencyKeyReused);
            }

            return (existing, true);
        }

        var evaluation = await ValidateAsync(context, attendees);
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

        return (await _bookingRepository.InsertConfirmedAsync(booking), false);
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

    private async Task<BookingEvaluation> ValidateAsync(BookingContext context, int attendees)
    {
        var rules = _constraintResolver.Resolve(context.Building, context.Floor, context.Space);
        var overrides = await LoadOverlappingOverridesAsync(context);
        var overlaps = await _bookingRepository.AnyConfirmedOverlapAsync(context.Space.Id, context.StartUtc, context.EndUtc);

        var violations = _validator.Validate(
            rules,
            context.LocalClock,
            new BookingRequest(context.StartUtc, context.EndUtc, attendees),
            overrides,
            overlaps,
            new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero),
            _options.SlotMinutes);

        return new BookingEvaluation(
            context.Space, context.Floor, context.Building, context.LocalClock, rules, context.StartUtc, context.EndUtc, violations);
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
        DateTimeOffset EndUtc);
}
