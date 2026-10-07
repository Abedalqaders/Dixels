using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Users;

namespace Dixels.Bookings;

/// <summary>An upcoming booking that a change would no longer allow, and which rules it would break.</summary>
public sealed record BookingImpact(Booking Booking, Space Space, Floor Floor, IReadOnlyList<BookingViolation> Violations);

/// <summary>
/// An upcoming booking as a preview reads it: only the columns a check or a description needs
/// (not the stored rules snapshot), and not tracked — previews never save.
/// </summary>
public sealed record UpcomingBooking(Guid Id, Guid SpaceId, Guid UserId, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Attendees);

/// <summary>An upcoming booking a change would affect, with the rules it would break (none for a delete or a move).</summary>
public sealed record BookingMisfit(UpcomingBooking Booking, IReadOnlyList<BookingViolation> Violations);

/// <summary>
/// "Which upcoming bookings would this change break?" — for an admin about to tighten a
/// rule, add a closure or remove a room. Each booking is checked by the same validator a
/// new booking runs, against the proposed rules, but only for the lasting rules (hours,
/// days, length, capacity, minimum people, closures): notice and how-far-ahead only matter
/// at the moment of booking, and a booking already made has passed them.
///
/// Then, if the admin chooses, cancels them as an admin cancel — the rows stay (history),
/// with who, when and why, and the slots are free straight away.
/// </summary>
public class BookingImpactChecker : DomainService
{
    private static readonly HashSet<string> LastingRules = new()
    {
        DixelsDomainErrorCodes.BookingSpaceClosed,
        DixelsDomainErrorCodes.BookingOverCapacity,
        DixelsDomainErrorCodes.BookingBelowMinAttendees,
        DixelsDomainErrorCodes.BookingTooLong,
        DixelsDomainErrorCodes.BookingClosedDay,
        DixelsDomainErrorCodes.BookingOutsideHours,
    };

    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<AvailabilityOverride, Guid> _overrideRepository;
    private readonly BookingPolicyValidator _validator;
    private readonly BookingOptions _options;
    private readonly ILocalEventBus _localEventBus;

    public BookingImpactChecker(
        IBookingRepository bookingRepository,
        IRepository<AvailabilityOverride, Guid> overrideRepository,
        BookingPolicyValidator validator,
        IOptions<BookingOptions> options,
        ILocalEventBus localEventBus)
    {
        _bookingRepository = bookingRepository;
        _overrideRepository = overrideRepository;
        _validator = validator;
        _options = options.Value;
        _localEventBus = localEventBus;
    }

    // Only the previews' room lookups need these, so they're resolved when first used.
    private IRepository<Space, Guid> SpaceRepository => LazyServiceProvider.LazyGetRequiredService<IRepository<Space, Guid>>();
    private IRepository<Floor, Guid> FloorRepository => LazyServiceProvider.LazyGetRequiredService<IRepository<Floor, Guid>>();
    private IDataFilter DataFilter => LazyServiceProvider.LazyGetRequiredService<IDataFilter>();

    /// <summary>
    /// The upcoming bookings on <paramref name="rooms"/> that <paramref name="proposedRules"/>
    /// (and <paramref name="addedClosure"/>, if any) would reject, as tracked entities — for
    /// cancelling them. Checked on <see cref="FindMisfitsAsync"/>'s light rows; only the ones
    /// that fail are then loaded whole.
    /// </summary>
    public async Task<IReadOnlyList<BookingImpact>> FindNoLongerFittingAsync(
        Building building,
        IReadOnlyList<(Space Space, Floor Floor)> rooms,
        Func<Space, Floor, ResolvedConstraints> proposedRules,
        OverrideWindow? addedClosure = null)
    {
        var misfits = await FindMisfitsAsync(building, rooms, proposedRules, addedClosure);
        if (misfits.Count == 0)
        {
            return Array.Empty<BookingImpact>();
        }

        var ids = misfits.Select(m => m.Booking.Id).ToList();
        var bookings = (await _bookingRepository.GetListAsync(b => ids.Contains(b.Id))).ToDictionary(b => b.Id);
        var roomById = rooms.ToDictionary(r => r.Space.Id);
        return misfits
            .Where(m => bookings.ContainsKey(m.Booking.Id))
            .Select(m => new BookingImpact(bookings[m.Booking.Id], roomById[m.Booking.SpaceId].Space, roomById[m.Booking.SpaceId].Floor, m.Violations))
            .ToList();
    }

    /// <summary>
    /// The upcoming bookings on <paramref name="rooms"/> that <paramref name="proposedRules"/>
    /// (and <paramref name="addedClosure"/>, if any) would reject, soonest first. Pass the
    /// proposed rules as a function so the caller can build them from unsaved copies — nothing
    /// here saves. Every upcoming booking in the rooms is checked (the rules are worked out in
    /// the building's time zone, which SQL can't do), but only the columns the check needs are
    /// read.
    /// </summary>
    public async Task<IReadOnlyList<BookingMisfit>> FindMisfitsAsync(
        Building building,
        IReadOnlyList<(Space Space, Floor Floor)> rooms,
        Func<Space, Floor, ResolvedConstraints> proposedRules,
        OverrideWindow? addedClosure = null)
    {
        var spaceIds = rooms.Select(r => r.Space.Id).ToList();
        if (spaceIds.Count == 0)
        {
            return Array.Empty<BookingMisfit>();
        }

        var now = Now();
        var bookings = await _bookingRepository.GetQueryableAsync();
        var upcoming = await AsyncExecuter.ToListAsync(Soonest(bookings
            .Where(b => spaceIds.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.StartsAt > now)));
        if (upcoming.Count == 0)
        {
            return Array.Empty<BookingMisfit>();
        }

        var until = upcoming.Max(b => b.EndsAt);
        var clock = new BuildingClock(building.Timezone);
        var roomById = rooms.ToDictionary(r => r.Space.Id);

        // Existing closures and special openings over the whole range, loaded once and looked
        // up by what they cover (a room, a floor or the building), not rescanned per booking.
        var floorIds = rooms.Select(r => r.Floor.Id).Distinct().ToList();
        var overrides = (await _overrideRepository.GetListAsync(o =>
                ((o.Scope == OverrideScope.Building && o.ScopeId == building.Id)
                 || (o.Scope == OverrideScope.Floor && floorIds.Contains(o.ScopeId))
                 || (o.Scope == OverrideScope.Space && spaceIds.Contains(o.ScopeId)))
                && o.StartsAt < until
                && o.EndsAt > now))
            .ToLookup(o => o.ScopeId, OverrideWindow.From);

        var misfits = new List<BookingMisfit>();
        foreach (var booking in upcoming)
        {
            var (space, floor) = roomById[booking.SpaceId];
            var relevant = overrides[space.Id]
                .Concat(overrides[floor.Id])
                .Concat(overrides[building.Id])
                .Append(addedClosure)
                .OfType<OverrideWindow>()
                .Where(o => o.Range.Overlaps(booking.StartsAt, booking.EndsAt))
                .ToList();

            var broken = _validator.Validate(
                    proposedRules(space, floor),
                    clock,
                    new BookingRequest(booking.StartsAt, booking.EndsAt, booking.Attendees),
                    relevant,
                    overlapsExistingBooking: false,
                    now,
                    _options.SlotMinutes)
                .Where(v => LastingRules.Contains(v.Code))
                .ToList();

            if (broken.Count > 0)
            {
                misfits.Add(new BookingMisfit(booking, broken));
            }
        }

        return misfits;
    }

    /// <summary>Every confirmed booking on these rooms that hasn't started yet, earliest first.</summary>
    public async Task<List<Booking>> FindUpcomingAsync(IReadOnlyCollection<Guid> spaceIds)
    {
        if (spaceIds.Count == 0)
        {
            return new List<Booking>();
        }

        var now = Now();
        return (await _bookingRepository.GetListAsync(b =>
                spaceIds.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.StartsAt > now))
            .OrderBy(b => b.StartsAt)
            .ToList();
    }

    /// <summary>
    /// The next <paramref name="maxCount"/> confirmed bookings on these rooms that haven't
    /// started yet, earliest first — for working through many a batch at a time (each batch
    /// cancelled drops out of the next).
    /// </summary>
    public async Task<List<Booking>> FindUpcomingAsync(IReadOnlyCollection<Guid> spaceIds, int maxCount)
    {
        if (spaceIds.Count == 0)
        {
            return new List<Booking>();
        }

        var now = Now();
        var bookings = await _bookingRepository.GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(bookings
            .Where(b => spaceIds.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.StartsAt > now)
            .OrderBy(b => b.StartsAt)
            .Take(maxCount));
    }

    /// <summary>How many confirmed bookings in <paramref name="scope"/> haven't started yet — one COUNT, no room loaded.</summary>
    public async Task<int> CountUpcomingAsync(RoomScope scope)
    {
        var now = Now();
        var rooms = await RoomIdsAsync(scope);
        var bookings = await _bookingRepository.GetQueryableAsync();
        return await AsyncExecuter.CountAsync(bookings.Where(b =>
            rooms.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.StartsAt > now));
    }

    /// <summary>
    /// The first <paramref name="maxCount"/> confirmed bookings in <paramref name="scope"/> that
    /// haven't started yet, soonest first: the rooms are found in the same query, never loaded.
    /// </summary>
    public async Task<List<UpcomingBooking>> FindUpcomingAsync(RoomScope scope, int maxCount)
    {
        var now = Now();
        var rooms = await RoomIdsAsync(scope);
        var bookings = await _bookingRepository.GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(Soonest(bookings
                .Where(b => rooms.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.StartsAt > now))
            .Take(maxCount));
    }

    /// <summary>How many confirmed bookings this person has in <paramref name="buildingId"/> that haven't started yet.</summary>
    public async Task<int> CountUpcomingForUserAsync(Guid userId, Guid buildingId)
    {
        using (DataFilter.Disable<ISoftDelete>())
        {
            return await AsyncExecuter.CountAsync(await UpcomingForUserQueryAsync(userId, buildingId));
        }
    }

    /// <summary>The first <paramref name="maxCount"/> of them, soonest first.</summary>
    public async Task<List<UpcomingBooking>> FindUpcomingForUserAsync(Guid userId, Guid buildingId, int maxCount)
    {
        using (DataFilter.Disable<ISoftDelete>())
        {
            return await AsyncExecuter.ToListAsync(Soonest(await UpcomingForUserQueryAsync(userId, buildingId)).Take(maxCount));
        }
    }

    // Rooms in deleted floors or buildings count too (the soft-delete filter is off around the
    // callers): they're still where the booking is, and a move releases it.
    private async Task<IQueryable<Booking>> UpcomingForUserQueryAsync(Guid userId, Guid buildingId)
    {
        var now = Now();
        var rooms =
            from s in await SpaceRepository.GetQueryableAsync()
            join f in await FloorRepository.GetQueryableAsync() on s.FloorId equals f.Id
            where f.BuildingId == buildingId
            select s.Id;
        var bookings = await _bookingRepository.GetQueryableAsync();
        // EndsAt > now is implied by StartsAt > now; it's there so the (UserId, EndsAt) index can seek.
        return bookings.Where(b =>
            b.UserId == userId && b.Status == BookingStatus.Confirmed && b.StartsAt > now && b.EndsAt > now
            && rooms.Contains(b.SpaceId));
    }

    // Space management's own room query: the same rooms its saves and deletes name.
    private Task<IQueryable<Guid>> RoomIdsAsync(RoomScope scope) =>
        LazyServiceProvider.LazyGetRequiredService<RoomIdReader>().QueryAsync(scope);

    /// <summary>
    /// Soonest first, then by id so the order is the same on every call (a preview is read a
    /// page at a time), as the light rows a preview reads.
    /// </summary>
    private static IQueryable<UpcomingBooking> Soonest(IQueryable<Booking> bookings) =>
        bookings
            .OrderBy(b => b.StartsAt)
            .ThenBy(b => b.Id)
            .Select(b => new UpcomingBooking(b.Id, b.SpaceId, b.UserId, b.Title, b.StartsAt, b.EndsAt, b.Attendees));

    /// <summary>
    /// These bookings, by id, that are still confirmed and haven't started — what a check made
    /// moments ago found, less any that were cancelled or began since.
    /// </summary>
    public async Task<List<Booking>> FindUpcomingByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new List<Booking>();
        }

        var now = Now();
        return await _bookingRepository.GetListAsync(b =>
            ids.Contains(b.Id) && b.Status == BookingStatus.Confirmed && b.StartsAt > now);
    }

    /// <summary>Every confirmed booking this person has that hasn't started yet, earliest first.</summary>
    public async Task<List<Booking>> FindUpcomingForUserAsync(Guid userId)
    {
        var now = Now();
        return (await _bookingRepository.GetListAsync(b =>
                // EndsAt > now is implied by StartsAt > now; it's there so the (UserId, EndsAt) index can seek.
                b.UserId == userId && b.Status == BookingStatus.Confirmed && b.StartsAt > now && b.EndsAt > now))
            .OrderBy(b => b.StartsAt)
            .ToList();
    }

    /// <summary>
    /// Cancels bookings on an admin's behalf, with the reason employees will see, and announces
    /// them in one <see cref="BookingsCancelledEvent"/> (<c>ByAdmin</c>).
    /// </summary>
    public virtual async Task CancelAsAdminAsync(IReadOnlyCollection<Booking> bookings, Guid adminId, Func<Booking, string> reason)
    {
        if (bookings.Count == 0)
        {
            return;
        }

        var now = Now();
        foreach (var booking in bookings)
        {
            booking.Cancel(adminId, now, reason(booking), byAdmin: true);
        }

        await _bookingRepository.UpdateManyAsync(bookings, autoSave: true);
        var invitees = await _bookingRepository.GetInviteesAsync(bookings.Select(b => b.Id).ToList());
        await _localEventBus.PublishAsync(new BookingsCancelledEvent(bookings.ToList(), byAdmin: true, invitees));
    }

    /// <summary>
    /// The same as <see cref="CancelAsAdminAsync"/> for bookings named by id, in one UPDATE
    /// instead of loading and saving each — for a cancel of thousands. The rules
    /// <see cref="Booking.Cancel"/> enforces are repeated: only bookings still confirmed (and,
    /// as everywhere here, not started) are cancelled, and the reason is checked the same way.
    /// The ones actually cancelled are announced in one <see cref="BookingsCancelledEvent"/>.
    /// </summary>
    public virtual async Task<List<Booking>> CancelUpcomingAsAdminAsync(IReadOnlyCollection<Guid> ids, Guid adminId, string reason)
    {
        if (ids.Count == 0)
        {
            return new List<Booking>();
        }

        var checkedReason = Check.Length(reason?.Trim(), nameof(reason), BookingConsts.MaxCancelReasonLength);
        var currentUser = LazyServiceProvider.LazyGetRequiredService<ICurrentUser>();
        var cancelled = await _bookingRepository.CancelUpcomingAsAdminAsync(
            ids, adminId, Now(), checkedReason!, Clock.Now, currentUser.Id);
        if (cancelled.Count > 0)
        {
            await _localEventBus.PublishAsync(new BookingsCancelledEvent(cancelled, byAdmin: true));
        }

        return cancelled;
    }

    private DateTimeOffset Now() => new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
