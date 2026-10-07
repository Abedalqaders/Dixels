using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Dixels.Bookings;

public partial class BookingManager
{
    /// <summary>
    /// The owner changing the guests (and head count) of one booking that hasn't started. The
    /// guest list is checked like a new booking's; of the room's rules only the head count
    /// ones run, grandfathered (see <see cref="BookingPolicyValidator.ValidateHeadCount"/>) —
    /// the booking already holds its slot. A date of a series is changed with the series
    /// (<see cref="ChangeSeriesInviteesAsync"/>), so every date keeps the series' list.
    /// </summary>
    public async Task<(Booking Booking, BookingPlace Place)> ChangeInviteesAsync(
        Guid userId, Guid bookingId, int attendees, IReadOnlyCollection<Invitee> invitees)
    {
        var booking = await _bookingRepository.FindAsync(bookingId);
        // Someone else's booking reads as "not found", as when reading it.
        if (booking is null || booking.UserId != userId)
        {
            throw new EntityNotFoundException(typeof(Booking), bookingId);
        }

        if (booking.SeriesId is not null)
        {
            throw new BusinessException(DixelsDomainErrorCodes.EditSeriesGuests);
        }

        if (booking.Status != BookingStatus.Confirmed || booking.StartsAt <= Now())
        {
            throw new BusinessException(DixelsDomainErrorCodes.GuestsNotEditable);
        }

        await _bookingRepository.LockSpaceAsync(booking.SpaceId);
        var place = await LoadPlaceAsync(booking.SpaceId);
        var resolved = await CheckGuestsAsync(userId, place, booking.Attendees, attendees, invitees);

        var (added, removed) = booking.ChangeGuests(attendees, resolved, GuidGenerator);
        await _bookingRepository.UpdateAsync(booking, autoSave: true);

        if (added.Count > 0 || removed.Count > 0)
        {
            await _localEventBus.PublishAsync(new BookingInviteesChangedEvent(booking.Id, null, new[] { booking }, added, removed));
        }

        return (booking, place);
    }

    /// <summary>
    /// The same for a whole series: its own list and every upcoming confirmed date, in one
    /// save. Dates under way, over or cancelled keep the guests they had.
    /// </summary>
    public async Task<(BookingSeries Series, IReadOnlyList<Booking> Bookings, BookingPlace Place)> ChangeSeriesInviteesAsync(
        Guid userId, Guid seriesId, int attendees, IReadOnlyCollection<Invitee> invitees)
    {
        var series = await _seriesRepository.FindAsync(seriesId);
        if (series is null || series.UserId != userId)
        {
            throw new EntityNotFoundException(typeof(BookingSeries), seriesId);
        }

        var now = Now();
        var upcoming = (await _bookingRepository.GetListAsync(
                b => b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed && b.StartsAt > now,
                includeDetails: true))
            .OrderBy(b => b.StartsAt)
            .ToList();
        if (upcoming.Count == 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.GuestsNotEditable);
        }

        await _bookingRepository.LockSpaceAsync(series.SpaceId);
        var place = await LoadPlaceAsync(series.SpaceId);
        var resolved = await CheckGuestsAsync(userId, place, series.Attendees, attendees, invitees);

        var (added, removed) = series.ChangeGuests(attendees, resolved, GuidGenerator);
        foreach (var booking in upcoming)
        {
            booking.ChangeGuests(attendees, resolved, GuidGenerator);
        }

        await _seriesRepository.UpdateAsync(series);
        await _bookingRepository.UpdateManyAsync(upcoming, autoSave: true);

        if (added.Count > 0 || removed.Count > 0)
        {
            await _localEventBus.PublishAsync(new BookingInviteesChangedEvent(null, series.Id, upcoming, added, removed));
        }

        return (series, upcoming, place);
    }

    /// <summary>The guest list as it will be saved, or a rejection naming what's wrong with it or the head count.</summary>
    private async Task<IReadOnlyList<Invitee>> CheckGuestsAsync(
        Guid userId, BookingPlace place, int storedAttendees, int attendees, IReadOnlyCollection<Invitee> invitees)
    {
        var resolved = await _inviteeResolver.ResolveAsync(userId, place.Building.Id, invitees);

        var rules = _constraintResolver.Resolve(place.Building, place.Floor, place.Space);
        var violations = _validator.ValidateHeadCount(rules, storedAttendees, attendees, resolved.Count);
        if (violations.Count > 0)
        {
            throw new BookingRejectedException(violations);
        }

        return resolved;
    }

    /// <summary>The room, its floor and building (with their names, for the reply).</summary>
    private async Task<BookingPlace> LoadPlaceAsync(Guid spaceId)
    {
        var space = await _spaceRepository.GetAsync(spaceId);
        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        return new BookingPlace(space, floor, building);
    }

    private DateTimeOffset Now() => new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
