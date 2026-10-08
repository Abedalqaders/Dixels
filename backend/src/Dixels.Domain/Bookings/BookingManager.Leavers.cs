using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;

namespace Dixels.Bookings;

public partial class BookingManager
{
    /// <summary>How many of a leaver's invitations are read and saved at a time.</summary>
    public const int LeaverBatchSize = 200;

    /// <summary>
    /// A colleague who left (deactivated, removed, or moved out of <paramref name="onlyInBuildingId"/>)
    /// comes off the guest list of every upcoming booking they're invited to, all of them or only
    /// that building's, and off those bookings' series. Bookings under way, over or cancelled keep
    /// them (that's history), and so does a series with no upcoming date. Silent: no event, so
    /// nobody is emailed; the head count stays, since it may count people who aren't named.
    /// </summary>
    public async Task RemoveGuestEverywhereAsync(Guid userId, Guid? onlyInBuildingId = null)
    {
        var now = Now();
        var invitations = await _bookingRepository.GetUpcomingInvitationsAsync(userId, now, onlyInBuildingId);

        foreach (var batch in invitations.Select(i => i.BookingId).Chunk(LeaverBatchSize))
        {
            await RetryOnceAsync(batch, Array.Empty<Guid>(), () => RemoveFromBookingsAsync(userId, batch, now));
        }

        // A series' own list is the same as its upcoming dates' (guests are only changed for the
        // whole series), so the series to change are those of the bookings just found.
        var seriesIds = invitations.Where(i => i.SeriesId is not null).Select(i => i.SeriesId!.Value).Distinct();
        foreach (var batch in seriesIds.Chunk(LeaverBatchSize))
        {
            await RetryOnceAsync(Array.Empty<Guid>(), batch, () => RemoveFromSeriesAsync(userId, batch));
        }
    }

    private async Task RemoveFromBookingsAsync(Guid userId, IReadOnlyCollection<Guid> ids, DateTimeOffset now)
    {
        // Read again, with their guests: one may have been cancelled since the ids were found.
        var bookings = await _bookingRepository.GetListAsync(
            b => ids.Contains(b.Id) && b.Status == BookingStatus.Confirmed && b.StartsAt > now,
            includeDetails: true);

        var changed = new List<Booking>();
        foreach (var booking in bookings)
        {
            if (booking.RemoveInvitee(userId))
            {
                changed.Add(booking);
            }
        }

        if (changed.Count > 0)
        {
            await _bookingRepository.UpdateManyAsync(changed, autoSave: true);
        }
    }

    private async Task RemoveFromSeriesAsync(Guid userId, IReadOnlyCollection<Guid> ids)
    {
        var series = await _seriesRepository.GetListAsync(s => ids.Contains(s.Id), includeDetails: true);

        var changed = new List<BookingSeries>();
        foreach (var one in series)
        {
            if (one.RemoveInvitee(userId))
            {
                changed.Add(one);
            }
        }

        if (changed.Count > 0)
        {
            await _seriesRepository.UpdateManyAsync(changed, autoSave: true);
        }
    }

    // An owner saving the same guests at that moment wins the race (the concurrency stamp), and
    // the admin's account change shouldn't fail for it: forget what was read, read it again with
    // their change in, and try once more. Losing twice rolls the account change back.
    private async Task RetryOnceAsync(IReadOnlyCollection<Guid> bookingIds, IReadOnlyCollection<Guid> seriesIds, Func<Task> removeAsync)
    {
        try
        {
            await removeAsync();
        }
        catch (AbpDbConcurrencyException)
        {
            await _bookingRepository.ForgetAsync(bookingIds, seriesIds);
            await removeAsync();
        }
    }
}
