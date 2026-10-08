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

    private async Task InviteAsync(
        Guid ownerId,
        Guid spaceId,
        IReadOnlyList<InviteeRow> guests,
        string? title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        BookingSeries? series,
        IReadOnlyCollection<Booking>? dates)
    {
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

            var colleagueIds = guests.Where(g => g.UserId is not null).Select(g => g.UserId!.Value).ToList();
            var colleagues = (await _userRepository.GetListByIdsAsync(colleagueIds)).ToDictionary(u => u.Id);

            var space = await _spaceRepository.GetAsync(spaceId, includeDetails: true);
            var floor = await _floorRepository.GetAsync(space.FloorId, includeDetails: true);
            var building = await _buildingRepository.GetAsync(floor.BuildingId, includeDetails: true);
            var clock = new BuildingClock(building.Timezone);
            var skipped = series is null ? new List<DateOnly>() : Skipped(series, dates!, clock);

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
                        Status = EmailStatus.Invitation,
                        RecipientName = name,
                        InvitedBy = ownerName,
                        CanOpen = colleague,
                        FindUrl = AppLink("/find-space"),
                        Footer = _localizer["Email:Invite:Footer", ownerName],
                    };
                    await NamePlaceAsync(model, space, floor, building);
                    Describe(model, clock, startsAt, endsAt, title);
                    model.AlsoInvited = OthersNames(guests, guest, colleagues);
                    model.Heading = _localizer["Email:Invite:Heading", ownerName, TitleOrRoom(model)];

                    string subject;
                    if (series is null)
                    {
                        var startTime = BookingFormat.Clock(TimeOnly.FromDateTime(clock.ToLocal(startsAt)));
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
                        new IcsPerson(fromName, rsvpMailbox), new IcsPerson(name, to));

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
