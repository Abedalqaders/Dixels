using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Emails.Rsvp;

/// <summary>What became of one answer read from the rsvp@ mailbox.</summary>
public enum RsvpReplyOutcome
{
    Applied,

    /// <summary>No guest has that UID any more (taken off the list, cleaned up) or never did.</summary>
    UnknownGuest,

    /// <summary>The meeting has started or was cancelled: answers are closed.</summary>
    Closed,

    /// <summary>The guest answered again since (in the app, or by link): latest answer wins.</summary>
    Stale,
}

/// <summary>
/// Applies one answer from a guest's mail app. The guest is found by the invite's UID alone —
/// each guest's copy has its own secret one, so it says who answered; the sender's address is
/// never trusted. The answer then goes through <see cref="BookingResponses.RespondAsGuestAsync"/>,
/// the same path as the app and the guest's link: that raises the one event the booker's
/// "declined" email listens to, so nothing is emailed from here.
/// </summary>
public class RsvpReplyApplier : DomainService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<BookingSeries, Guid> _seriesRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly BookingResponses _bookingResponses;

    public RsvpReplyApplier(
        IBookingRepository bookingRepository,
        IRepository<BookingSeries, Guid> seriesRepository,
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        BookingResponses bookingResponses)
    {
        _bookingRepository = bookingRepository;
        _seriesRepository = seriesRepository;
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _bookingResponses = bookingResponses;
    }

    public async Task<RsvpReplyOutcome> ApplyAsync(ItipAnswer answer)
    {
        var target = await FindAsync(answer);
        if (target is null)
        {
            return RsvpReplyOutcome.UnknownGuest;
        }

        // A reply that sat in the mailbox (the reader was down) mustn't undo a newer answer. A
        // reply's stamp has whole seconds only, so an answer within the same second isn't newer.
        var answeredAt = new DateTimeOffset(answer.AnsweredAtUtc, TimeSpan.Zero);
        if (target.Row.RespondedAt is { } previous && previous - answeredAt >= TimeSpan.FromSeconds(1))
        {
            return RsvpReplyOutcome.Stale;
        }

        if (!await _bookingResponses.IsOpenAsync(target))
        {
            return RsvpReplyOutcome.Closed;
        }

        await _bookingResponses.RespondAsGuestAsync(target, answer.Status);
        return RsvpReplyOutcome.Applied;
    }

    /// <summary>
    /// The guest the UID belongs to: a booking's guest (that date), a series' guest (the whole
    /// series), or — a series' UID with a RECURRENCE-ID — that guest on the one date answered.
    /// </summary>
    private async Task<GuestLinkTarget?> FindAsync(ItipAnswer answer)
    {
        var booking = await _bookingRepository.FindAsync(b => b.Invitees.Any(i => i.IcsUid == answer.Uid), includeDetails: true);
        if (booking is not null)
        {
            return new GuestLinkTarget(booking.Invitees.Single(i => i.IcsUid == answer.Uid), booking, null);
        }

        var series = await _seriesRepository.FindAsync(s => s.Invitees.Any(i => i.IcsUid == answer.Uid), includeDetails: true);
        if (series is null)
        {
            return null;
        }

        var seriesRow = series.Invitees.Single(i => i.IcsUid == answer.Uid);
        if (answer.Occurrence is not { } occurrence)
        {
            return new GuestLinkTarget(seriesRow, null, series);
        }

        // One date of the series: the RECURRENCE-ID is its original start, in the building's
        // zone as we sent it (or UTC, if the mail app converted it).
        var start = answer.OccurrenceIsUtc
            ? new DateTimeOffset(DateTime.SpecifyKind(occurrence, DateTimeKind.Utc))
            : (await ClockOfSpaceAsync(series.SpaceId)).ToUtc(DateTime.SpecifyKind(occurrence, DateTimeKind.Unspecified));
        var date = await _bookingRepository.FindAsync(b => b.SeriesId == series.Id && b.StartsAt == start, includeDetails: true);
        var key = seriesRow.ToInvitee().Key;
        var dateRow = date?.Invitees.FirstOrDefault(i => i.ToInvitee().Key == key);
        if (dateRow is null)
        {
            Logger.LogInformation("An answer for a date of series {SeriesId} that isn't booked, or the guest isn't on, was dropped.", series.Id);
            return null;
        }

        return new GuestLinkTarget(dateRow, date, null);
    }

    /// <summary>The building clock of a room — its timezone only (deleted rooms included).</summary>
    private async Task<BuildingClock> ClockOfSpaceAsync(Guid spaceId)
    {
        using (LazyServiceProvider.LazyGetRequiredService<IDataFilter>().Disable<ISoftDelete>())
        {
            var timezone = await AsyncExecuter.FirstAsync(
                from s in await _spaceRepository.GetQueryableAsync()
                join f in await _floorRepository.GetQueryableAsync() on s.FloorId equals f.Id
                join b in await _buildingRepository.GetQueryableAsync() on f.BuildingId equals b.Id
                where s.Id == spaceId
                select b.Timezone);
            return new BuildingClock(timezone);
        }
    }
}
