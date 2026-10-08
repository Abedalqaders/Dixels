using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Settings;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Localization;

namespace Dixels.Emails;

/// <summary>
/// One meeting a guest has lost to a cancel — what one email (and one CANCEL file) is about:
/// a single booking, or one or more dates of a series, or the whole series. The UID is the
/// one their invite carried, so their mail app takes exactly that event (or those dates) off.
/// Serializable: an admin's cancels are written later, by <see cref="AdminCancelledGuestEmailJob"/>.
/// </summary>
public class GuestCancelNotice
{
    public string IcsUid { get; set; } = string.Empty;

    /// <summary>The SEQUENCE this CANCEL carries (the guest's copy, one up).</summary>
    public int Sequence { get; set; }

    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>A series' dates (or the whole series): its id. Null for a single booking.</summary>
    public Guid? SeriesId { get; set; }

    /// <summary>The series has no date left: cancel the whole event, not date by date.</summary>
    public bool WholeSeries { get; set; }

    /// <summary>The cancelled bookings this guest was on: one, or a series' dates.</summary>
    public List<Guid> BookingIds { get; set; } = new();

    /// <summary>The booker took this guest off the list (the booking goes on), rather than cancelling.</summary>
    public bool Removed { get; set; }

    /// <summary>Who this is, to count each guest once (the user id, or the email).</summary>
    public string GuestKey => UserId?.ToString() ?? Invitee.NormalizeEmail(Email!);
}

public partial class BookingEmails
{
    /// <summary>
    /// Tells each guest of what the owner just cancelled: an email per meeting with a CANCEL
    /// file, written now and sent by <see cref="SendEmailJob"/>. Returns how many guests were
    /// told, for the owner's own email. Never throws.
    /// </summary>
    public async Task<int> SendGuestCancelsAsync(IReadOnlyCollection<Booking> cancelled)
    {
        try
        {
            var notices = await GuestCancelNoticesAsync(cancelled);
            foreach (var notice in notices)
            {
                if (await WriteGuestCancelAsync(notice, byAdmin: false) is { } email)
                {
                    await _backgroundJobManager.EnqueueAsync(email);
                }
            }

            return notices.Select(n => n.GuestKey).Distinct().Count();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not write the guests' cancel emails.");
            return 0;
        }
    }

    /// <summary>
    /// Who lost what in these cancels — one notice per guest per meeting — and the SEQUENCE
    /// of each of their copies moved one up (in the cancel's own unit of work, so a rollback
    /// leaves them as they were). The guests are those on the bookings when they were
    /// cancelled: anyone taken off earlier isn't told.
    /// </summary>
    public async Task<List<GuestCancelNotice>> GuestCancelNoticesAsync(IReadOnlyCollection<Booking> cancelled)
    {
        var notices = new List<GuestCancelNotice>();
        var bookingIds = cancelled.Select(b => b.Id).ToList();
        var bookingGuests = (await _bookingRepository.GetGuestRowsAsync(bookingIds)).ToLookup(g => g.BookingId);
        if (bookingGuests.Count == 0)
        {
            return notices;
        }

        var seriesIds = cancelled.Where(b => b.SeriesId is not null).Select(b => b.SeriesId!.Value).Distinct().ToList();
        var seriesGuests = (await _bookingRepository.GetSeriesGuestRowsAsync(seriesIds)).ToLookup(g => g.SeriesId);
        var stillOn = await _bookingRepository.GetSeriesWithUpcomingAsync(seriesIds, new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero));

        // Single bookings, and any series date a guest was on outside the series' own list:
        // a notice each, by the booking guest's own UID.
        GuestCancelNotice Single(Booking booking, InviteeRow row) => Notice(row, booking.UserId, null, false, new List<Guid> { booking.Id });

        foreach (var booking in cancelled.Where(b => b.SeriesId is null))
        {
            notices.AddRange(bookingGuests[booking.Id].Select(row => Single(booking, row)));
        }

        // A series' dates: one notice per series guest, for the dates they were on, by the
        // series guest's UID (the one their series invite carried).
        foreach (var dates in cancelled.Where(b => b.SeriesId is not null).GroupBy(b => b.SeriesId!.Value))
        {
            var bySeriesGuest = seriesGuests[dates.Key].ToDictionary(g => g.ToInvitee().Key);
            var lost = new Dictionary<string, (InviteeRow Row, List<Guid> Bookings)>();
            foreach (var booking in dates.OrderBy(b => b.StartsAt))
            {
                foreach (var row in bookingGuests[booking.Id])
                {
                    var key = row.ToInvitee().Key;
                    if (!bySeriesGuest.TryGetValue(key, out var seriesRow))
                    {
                        notices.Add(Single(booking, row));
                    }
                    else if (lost.TryGetValue(key, out var entry))
                    {
                        entry.Bookings.Add(booking.Id);
                    }
                    else
                    {
                        lost[key] = (seriesRow, new List<Guid> { booking.Id });
                    }
                }
            }

            var owner = dates.First().UserId;
            notices.AddRange(lost.Values.Select(l => Notice(l.Row, owner, dates.Key, !stillOn.Contains(dates.Key), l.Bookings)));
        }

        await _bookingRepository.BumpIcsSequenceAsync(notices.Select(n => n.IcsUid).Distinct().ToList());
        return notices;
    }

    private static GuestCancelNotice Notice(InviteeRow row, Guid ownerId, Guid? seriesId, bool wholeSeries, List<Guid> bookingIds) => new()
    {
        IcsUid = row.IcsUid,
        Sequence = row.IcsSequence + 1,
        UserId = row.UserId,
        Email = row.Email,
        Name = row.Name,
        OwnerId = ownerId,
        SeriesId = seriesId,
        WholeSeries = wholeSeries,
        BookingIds = bookingIds,
    };

    /// <summary>
    /// "Q4 planning is cancelled" for one guest, in their language (an outsider: the
    /// booker's), with the CANCEL file. Null when there's no one to tell: a colleague whose
    /// account is gone or switched off, or bookings no longer there.
    /// </summary>
    public async Task<SendEmailArgs?> WriteGuestCancelAsync(GuestCancelNotice notice, bool byAdmin)
    {
        var owner = await _userRepository.FindAsync(notice.OwnerId, includeDetails: false);
        if (owner is null)
        {
            return null;
        }

        string to, name, language;
        if (notice.UserId is { } userId)
        {
            var user = await _userRepository.FindAsync(userId, includeDetails: false);
            if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
            {
                return null;
            }

            (to, name, language) = (user.Email, DisplayName(user), await _userLanguage.GetAsync(userId));
        }
        else
        {
            (to, name, language) = (notice.Email!, string.IsNullOrWhiteSpace(notice.Name) ? notice.Email! : notice.Name!, await _userLanguage.GetAsync(notice.OwnerId));
        }

        var bookings = (await _bookingRepository.GetListAsync(b => notice.BookingIds.Contains(b.Id)))
            .OrderBy(b => b.StartsAt)
            .ToList();
        if (bookings.Count == 0)
        {
            return null;
        }

        var first = bookings[0];
        var series = notice.SeriesId is { } seriesId ? await _seriesRepository.FindAsync(seriesId) : null;

        // A removed room (or its floor or building) may be why it was cancelled: read it anyway.
        Space space;
        Floor floor;
        Building building;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            space = await _spaceRepository.GetAsync(first.SpaceId, includeDetails: true);
            floor = await _floorRepository.GetAsync(space.FloorId, includeDetails: true);
            building = await _buildingRepository.GetAsync(floor.BuildingId, includeDetails: true);
        }

        var clock = new BuildingClock(building.Timezone);
        var ownerName = DisplayName(owner);
        var fromName = string.Format(ViaDixels, ownerName);
        var rsvpMailbox = await _settingProvider.GetOrNullAsync(DixelsSettings.RsvpMailboxAddress) ?? "rsvp@dixels.local";

        using (CultureHelper.Use(language))
        {
            var model = new BookingEmailModel
            {
                Status = EmailStatus.Cancelled,
                Cancelled = true,
                CancelledByAdmin = byAdmin,
                RemovedByOwner = notice.Removed,
                RecipientName = name,
                InvitedBy = ownerName,
                Reason = byAdmin ? first.CancelReason : null,
                FindUrl = AppLink("/find-space"),
                Footer = _localizer["Email:Invite:Footer", ownerName],
            };
            await NamePlaceAsync(model, space, floor, building);
            Describe(model, clock, first.StartsAt, first.EndsAt, series?.Title ?? first.Title);

            string subject;
            if (notice.WholeSeries && series is not null)
            {
                model.Rows[0].Date = RepeatText(series.Rule, series.FirstDate);
                subject = _localizer["Email:GuestCancelled:SubjectSeries", TitleOrRoom(model)];
            }
            else
            {
                foreach (var booking in bookings.Skip(1))
                {
                    model.Rows.Add(Row(model, clock, booking.StartsAt, booking.EndsAt));
                }

                subject = _localizer["Email:GuestCancelled:Subject", TitleOrRoom(model), model.Date];
            }

            model.Heading = notice.Removed
                ? _localizer["Email:GuestRemoved:Heading", ownerName, TitleOrRoom(model)]
                : _localizer["Email:GuestCancelled:Heading", TitleOrRoom(model)];
            if (notice.Removed)
            {
                subject = _localizer["Email:GuestRemoved:Subject", TitleOrRoom(model), notice.WholeSeries ? model.Rows[0].Date : model.Date];
            }

            var calendar = Calendar(model, clock, notice.IcsUid, notice.Sequence, first.StartsAt, first.EndsAt,
                notice.WholeSeries ? series?.Rule : null, Array.Empty<DateOnly>(),
                new IcsPerson(fromName, rsvpMailbox), new IcsPerson(name, to)) with
            {
                Method = IcsMethods.Cancel,
                // Dates of a series that goes on: just those, by RECURRENCE-ID.
                LocalRecurrenceIds = notice.SeriesId is not null && !notice.WholeSeries
                    ? bookings.Select(b => clock.ToLocal(b.StartsAt)).ToList()
                    : Array.Empty<DateTime>(),
            };

            return new SendEmailArgs
            {
                To = to,
                Subject = subject,
                Body = await RenderAsync(DixelsEmailTemplates.GuestCancelled, model, language),
                ReplyTo = owner.Email,
                FromName = fromName,
                Ics = IcsBuilder.Build(calendar),
                IcsMethod = IcsMethods.Cancel,
            };
        }
    }
}
