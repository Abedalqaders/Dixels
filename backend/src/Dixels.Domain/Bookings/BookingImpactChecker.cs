using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Bookings;

/// <summary>An upcoming booking that a change would no longer allow, and which rules it would break.</summary>
public sealed record BookingImpact(Booking Booking, Space Space, Floor Floor, IReadOnlyList<BookingViolation> Violations);

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

    public BookingImpactChecker(
        IBookingRepository bookingRepository,
        IRepository<AvailabilityOverride, Guid> overrideRepository,
        BookingPolicyValidator validator,
        IOptions<BookingOptions> options)
    {
        _bookingRepository = bookingRepository;
        _overrideRepository = overrideRepository;
        _validator = validator;
        _options = options.Value;
    }

    /// <summary>
    /// The upcoming bookings on <paramref name="rooms"/> that <paramref name="proposedRules"/>
    /// (and <paramref name="addedClosure"/>, if any) would reject. Pass the proposed rules as
    /// a function so the caller can build them from unsaved copies — nothing here saves.
    /// </summary>
    public async Task<IReadOnlyList<BookingImpact>> FindNoLongerFittingAsync(
        Building building,
        IReadOnlyList<(Space Space, Floor Floor)> rooms,
        Func<Space, Floor, ResolvedConstraints> proposedRules,
        OverrideWindow? addedClosure = null)
    {
        var upcoming = await FindUpcomingAsync(rooms.Select(r => r.Space.Id).ToList());
        if (upcoming.Count == 0)
        {
            return Array.Empty<BookingImpact>();
        }

        var now = Now();
        var until = upcoming.Max(b => b.EndsAt);
        var clock = new BuildingClock(building.Timezone);
        var roomById = rooms.ToDictionary(r => r.Space.Id);

        // Existing closures and special openings over the whole range, loaded once.
        var spaceIds = rooms.Select(r => r.Space.Id).ToList();
        var floorIds = rooms.Select(r => r.Floor.Id).Distinct().ToList();
        var overrides = (await _overrideRepository.GetListAsync(o =>
                ((o.Scope == OverrideScope.Building && o.ScopeId == building.Id)
                 || (o.Scope == OverrideScope.Floor && floorIds.Contains(o.ScopeId))
                 || (o.Scope == OverrideScope.Space && spaceIds.Contains(o.ScopeId)))
                && o.StartsAt < until
                && o.EndsAt > now))
            .Select(o => (Window: OverrideWindow.From(o), o.ScopeId))
            .ToList();

        var impacts = new List<BookingImpact>();
        foreach (var booking in upcoming)
        {
            var (space, floor) = roomById[booking.SpaceId];
            var relevant = overrides
                .Where(o => o.ScopeId == space.Id || o.ScopeId == floor.Id || o.ScopeId == building.Id)
                .Select(o => o.Window)
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
                impacts.Add(new BookingImpact(booking, space, floor, broken));
            }
        }

        return impacts;
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

    /// <summary>Cancels bookings on an admin's behalf, with the reason employees will see.</summary>
    public async Task CancelAsAdminAsync(IReadOnlyCollection<Booking> bookings, Guid adminId, Func<Booking, string> reason)
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
    }

    private DateTimeOffset Now() => new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
