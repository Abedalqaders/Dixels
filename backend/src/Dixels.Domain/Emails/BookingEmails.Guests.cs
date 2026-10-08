using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Localization;
using Dixels.Settings;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Logging;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Settings;

namespace Dixels.Emails;

/// <summary>
/// The emails a booking's guests get, and the parts of the booker's own emails that are
/// about guests. Every guest gets their own email — never one to everyone — in their own
/// language (an outside guest: the booker's), with their own calendar file: its UID is the
/// guest's secret <see cref="InviteeRow.IcsUid"/>, so later updates and cancels replace it.
/// Written when the booking saves and sent by <see cref="SendEmailJob"/>, which drops it if
/// the guest has been taken off the list by then.
/// </summary>
public partial class BookingEmails
{
    /// <summary>How the sender is named on guests' emails and calendar files.</summary>
    private const string ViaDixels = "{0} (via Dixels)";

    /// <summary>"Sara Ali invited you" — one email to each guest of a new booking.</summary>
    public Task SendInvitesAsync(Booking booking) =>
        InviteAsync(booking.UserId, booking.SpaceId, booking.Invitees.ToList(), booking.Title, booking.StartsAt, booking.EndsAt, null, null);

    /// <summary>One invite to each guest for a whole new series — not one per date.</summary>
    public Task SendSeriesInvitesAsync(BookingSeries series, IReadOnlyCollection<Booking> bookings)
    {
        if (bookings.Count == 0)
        {
            return Task.CompletedTask;
        }

        var first = bookings.MinBy(b => b.StartsAt)!;
        return InviteAsync(series.UserId, series.SpaceId, series.Invitees.ToList(), series.Title, first.StartsAt, first.EndsAt, series, bookings);
    }

    /// <summary>The emails <see cref="InviteAsync"/> writes to guests.</summary>
    private enum GuestEmail
    {
        /// <summary>"Sara Ali invited you", with their calendar file.</summary>
        Invite,

        /// <summary>"Sara Ali added you" — the same, for someone added to an existing list.</summary>
        Added,

        /// <summary>"Q4 planning starts at 10:00" — a plain email, no calendar file.</summary>
        Reminder,
    }

    /// <summary>
    /// "Q4 planning starts soon" to each guest of a booking due its reminder, at the same time
    /// as the booker's (see <see cref="BookingReminders"/>). A plain email — no calendar file,
    /// so no second event in their calendar. Guests who declined aren't reminded.
    /// </summary>
    public async Task SendGuestRemindersAsync(Booking booking)
    {
        var everyone = (await _bookingRepository.GetGuestRowsAsync(new[] { booking.Id })).Cast<InviteeRow>().ToList();
        var toRemind = everyone.Where(g => g.ResponseStatus != InviteeResponseStatus.Declined).ToList();
        await InviteAsync(booking.UserId, booking.SpaceId, toRemind, booking.Title, booking.StartsAt, booking.EndsAt, null, null,
            everyone, GuestEmail.Reminder);
    }

    /// <summary>
    /// Writes <paramref name="kind"/> to <paramref name="guests"/> — all of a new booking's,
    /// only those just added, or those to remind; <paramref name="everyone"/> is the whole
    /// list, for the names of the others.
    /// </summary>
    private async Task InviteAsync(
        Guid ownerId,
        Guid spaceId,
        IReadOnlyList<InviteeRow> guests,
        string? title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        BookingSeries? series,
        IReadOnlyCollection<Booking>? dates,
        IReadOnlyList<InviteeRow>? everyone = null,
        GuestEmail kind = GuestEmail.Invite)
    {
        everyone ??= guests;
        if (guests.Count == 0)
        {
            return;
        }

        try
        {
            var owner = await _userRepository.FindAsync(ownerId, includeDetails: false);
            if (owner is null)
            {
                return;
            }

            var ownerName = DisplayName(owner);
            var fromName = string.Format(ViaDixels, ownerName);
            var ownerLanguage = await _userLanguage.GetAsync(ownerId);
            var rsvpMailbox = await _settingProvider.GetOrNullAsync(DixelsSettings.RsvpMailboxAddress) ?? "rsvp@dixels.local";

            var colleagueIds = everyone.Where(g => g.UserId is not null).Select(g => g.UserId!.Value).ToList();
            var colleagues = (await _userRepository.GetListByIdsAsync(colleagueIds)).ToDictionary(u => u.Id);

            var space = await _spaceRepository.GetAsync(spaceId, includeDetails: true);
            var floor = await _floorRepository.GetAsync(space.FloorId, includeDetails: true);
            var building = await _buildingRepository.GetAsync(floor.BuildingId, includeDetails: true);
            var clock = new BuildingClock(building.Timezone);
            // A series joined part-way starts at its next date: no dates before that one.
            var from = clock.LocalDate(startsAt);
            var skipped = series is null ? new List<DateOnly>() : Skipped(series, dates!, clock).Where(d => d >= from).ToList();

            foreach (var guest in guests)
            {
                // Where it goes and in which language: a colleague's own, an outsider the booker's.
                string to, name, language;
                var colleague = guest.UserId is { } userId;
                if (colleague)
                {
                    if (!colleagues.TryGetValue(guest.UserId!.Value, out var user) || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
                    {
                        continue;
                    }

                    (to, name, language) = (user.Email, DisplayName(user), await _userLanguage.GetAsync(user.Id));
                }
                else
                {
                    (to, name, language) = (guest.Email!, string.IsNullOrWhiteSpace(guest.Name) ? guest.Email! : guest.Name!, ownerLanguage);
                }

                using (CultureHelper.Use(language))
                {
                    var model = new BookingEmailModel
                    {
                        Status = kind == GuestEmail.Reminder ? EmailStatus.Reminder : EmailStatus.Invitation,
                        RecipientName = name,
                        InvitedBy = ownerName,
                        CanOpen = colleague,
                        FindUrl = AppLink("/find-space"),
                        Footer = _localizer["Email:Invite:Footer", ownerName],
                    };
                    await NamePlaceAsync(model, space, floor, building);
                    Describe(model, clock, startsAt, endsAt, title);
                    model.AlsoInvited = OthersNames(everyone, guest, colleagues);
                    var startTime = BookingFormat.Clock(TimeOnly.FromDateTime(clock.ToLocal(startsAt)));
                    if (kind == GuestEmail.Reminder)
                    {
                        model.Heading = _localizer["Email:BookingReminder:Heading", TitleOrRoom(model), startTime];
                        await _backgroundJobManager.EnqueueAsync(new SendEmailArgs
                        {
                            To = to,
                            Subject = _localizer["Email:GuestReminder:Subject", TitleOrRoom(model), startTime],
                            Body = await RenderAsync(DixelsEmailTemplates.GuestReminder, model, language),
                            ReplyTo = owner.Email,
                            FromName = fromName,
                            GuestIcsUid = guest.IcsUid,
                        });
                        continue;
                    }

                    model.Heading = _localizer[kind == GuestEmail.Added ? "Email:Invite:AddedHeading" : "Email:Invite:Heading", ownerName, TitleOrRoom(model)];

                    string subject;
                    if (series is null)
                    {
                        subject = _localizer["Email:Invite:Subject", TitleOrRoom(model), model.Date, startTime];
                    }
                    else
                    {
                        model.Rows[0].Date = RepeatText(series.Rule, series.FirstDate);
                        model.RepeatsFrom = _localizer["Email:RepeatsFrom", model.Date];
                        model.NotOn = skipped.Count == 0 ? null : Listed(skipped.Select(BookingFormat.Date), ShownSkippedDates);
                        model.IsSeries = true;
                        subject = _localizer["Email:Invite:SubjectSeries", TitleOrRoom(model), model.Date];
                    }

                    var calendar = Calendar(model, clock, guest.IcsUid, guest.IcsSequence, startsAt, endsAt, series?.Rule, skipped,
                        new IcsPerson(fromName, rsvpMailbox), new IcsPerson(name, to)) with { RuleAnchor = series?.FirstDate };

                    await _backgroundJobManager.EnqueueAsync(new SendEmailArgs
                    {
                        To = to,
                        Subject = subject,
                        Body = await RenderAsync(DixelsEmailTemplates.Invite, model, language),
                        ReplyTo = owner.Email,
                        FromName = fromName,
                        Ics = IcsBuilder.Build(calendar),
                        IcsMethod = calendar.Method,
                        GuestIcsUid = guest.IcsUid,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not write the guest invites for a booking by user {UserId}.", ownerId);
        }
    }

    /// <summary>
    /// The guest list was edited: "added you" to each new guest (with their own new calendar
    /// file), "removed you" to each one taken off (a CANCEL for the copy they had — the whole
    /// series, for a series). Those who stay, and the booker, hear nothing. Never throws.
    /// </summary>
    public async Task SendGuestChangesAsync(BookingInviteesChangedEvent change)
    {
        if (change.Bookings.Count == 0)
        {
            return;
        }

        var first = change.Bookings.MinBy(b => b.StartsAt)!;
        try
        {
            if (change.Added.Count > 0)
            {
                var addedKeys = change.Added.Select(i => i.Key).ToHashSet();
                if (change.SeriesId is { } seriesId)
                {
                    var series = await _seriesRepository.GetAsync(seriesId);
                    var everyone = await _bookingRepository.GetSeriesGuestRowsAsync(new[] { seriesId });
                    await InviteAsync(series.UserId, series.SpaceId, everyone.Where(r => addedKeys.Contains(r.ToInvitee().Key)).ToList(),
                        series.Title, first.StartsAt, first.EndsAt, series, change.Bookings, everyone, GuestEmail.Added);
                }
                else
                {
                    var everyone = first.Invitees.ToList();
                    await InviteAsync(first.UserId, first.SpaceId, everyone.Where(r => addedKeys.Contains(r.ToInvitee().Key)).ToList(),
                        first.Title, first.StartsAt, first.EndsAt, null, null, everyone, GuestEmail.Added);
                }
            }

            foreach (var copy in change.RemovedCopies)
            {
                var notice = new GuestCancelNotice
                {
                    IcsUid = copy.IcsUid,
                    Sequence = copy.IcsSequence + 1,
                    UserId = copy.Who.UserId,
                    Email = copy.Who.Email,
                    Name = copy.Who.Name,
                    OwnerId = first.UserId,
                    SeriesId = change.SeriesId,
                    // Off the meeting: the whole series leaves their calendar, as Outlook does.
                    WholeSeries = change.SeriesId is not null,
                    BookingIds = change.Bookings.Select(b => b.Id).ToList(),
                    Removed = true,
                };
                if (await WriteGuestCancelAsync(notice, byAdmin: false) is { } email)
                {
                    await _backgroundJobManager.EnqueueAsync(email);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not write the emails for a guest list change by user {UserId}.", first.UserId);
        }
    }

    /// <summary>
    /// The booker's own "Add to my calendar" file: PUBLISH, no one asked to answer, its UID the
    /// booking's (or series') id — the booker never replies, so it needn't be secret.
    /// </summary>
    private async Task<IcsEvent> OwnerCalendarAsync(
        BookingEmailModel model, BuildingClock clock, string uid, DateTimeOffset startsAt, DateTimeOffset endsAt,
        RecurrenceRule? rule, IReadOnlyCollection<DateOnly> skipped)
    {
        var rsvpMailbox = await _settingProvider.GetOrNullAsync(DixelsSettings.RsvpMailboxAddress) ?? "rsvp@dixels.local";
        return Calendar(model, clock, uid, 0, startsAt, endsAt, rule, skipped,
            new IcsPerson(string.Format(ViaDixels, model.RecipientName), rsvpMailbox), attendee: null);
    }

    private IcsEvent Calendar(
        BookingEmailModel model, BuildingClock clock, string uid, int sequence, DateTimeOffset startsAt, DateTimeOffset endsAt,
        RecurrenceRule? rule, IReadOnlyCollection<DateOnly> skipped, IcsPerson organizer, IcsPerson? attendee)
    {
        var localStart = clock.ToLocal(startsAt);
        return new IcsEvent
        {
            Uid = uid,
            Sequence = sequence,
            Method = IcsMethods.Publish,
            Summary = TitleOrRoom(model),
            Location = string.Join(" · ", new[] { model.SpaceName, model.FloorName, model.BuildingName, model.Address }
                .Where(part => !string.IsNullOrWhiteSpace(part))),
            TimeZoneId = clock.Zone.Id,
            LocalStart = localStart,
            LocalEnd = clock.ToLocal(endsAt),
            Rule = rule,
            LocalExceptions = skipped.Select(d => d.ToDateTime(TimeOnly.FromDateTime(localStart))).ToList(),
            Organizer = organizer,
            Attendee = attendee,
            StampUtc = Clock.Now.ToUniversalTime(),
        };
    }

    /// <summary>The dates a series' rule lands on that weren't booked (skipped, or broke a rule).</summary>
    private static List<DateOnly> Skipped(BookingSeries series, IReadOnlyCollection<Booking> bookings, BuildingClock clock)
    {
        var booked = bookings.Select(b => clock.LocalDate(b.StartsAt)).ToHashSet();
        return RecurrenceExpander.Expand(series.Rule, series.FirstDate, BookingConsts.MaxSeriesOccurrences)
            .Where(d => !booked.Contains(d))
            .ToList();
    }

    /// <summary>
    /// The booker's own list of their guests: names, and an outsider's email when they gave no
    /// name (the booker typed it). Outsiders are marked "(guest)". Null when there are none.
    /// </summary>
    private async Task<string?> GuestListForOwnerAsync(IEnumerable<Invitee> invitees)
    {
        var list = invitees.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var ids = list.Where(i => i.UserId is not null).Select(i => i.UserId!.Value).ToList();
        var users = (await _userRepository.GetListByIdsAsync(ids)).ToDictionary(u => u.Id);
        var names = list.Select(i => i.UserId is { } id
            ? users.TryGetValue(id, out var user) ? DisplayName(user) : null
            : _localizer["Email:GuestMark", string.IsNullOrWhiteSpace(i.Name) ? i.Email! : i.Name!].Value);
        return string.Join(_localizer["Email:ListSeparator"], names.Where(n => n is not null));
    }

    /// <summary>The other guests, by name only — never their emails. An outsider with no name is "a guest".</summary>
    private string? OthersNames(IReadOnlyList<InviteeRow> guests, InviteeRow me, IReadOnlyDictionary<Guid, IdentityUser> colleagues)
    {
        var names = guests
            .Where(g => g != me)
            .Select(g => g.UserId is { } id
                ? colleagues.TryGetValue(id, out var user) ? DisplayName(user) : null
                : string.IsNullOrWhiteSpace(g.Name) ? _localizer["Email:UnnamedGuest"].Value : g.Name)
            .Where(n => n is not null)
            .ToList();
        return names.Count == 0 ? null : string.Join(_localizer["Email:ListSeparator"], names);
    }

    /// <summary>
    /// The room, floor and building names in the current language, and the building's address:
    /// the shown name's, else the default language's (an English address beats none).
    /// </summary>
    private async Task NamePlaceAsync(BookingEmailModel model, Space space, Floor floor, Building building)
    {
        var shown = await _nameReader.ShownTranslationAsync(building);
        model.SpaceName = await _nameReader.ShownAsync(space);
        model.FloorName = await _nameReader.ShownAsync(floor);
        model.BuildingName = shown?.Name ?? string.Empty;
        model.Address = shown?.Address;
        if (string.IsNullOrWhiteSpace(model.Address))
        {
            var (_, fallback) = await _nameReader.GetLanguagesAsync();
            model.Address = building.FindAddress(fallback);
        }
    }
}
