using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Localization;
using Dixels.SpaceManagement;
using Dixels.Users;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.TextTemplating;

namespace Dixels.Emails;

/// <summary>
/// The emails an employee gets about their own bookings, each in their own language (see
/// <see cref="UserLanguageManager"/>) with times in the building's local time.
///
/// Emails are queued, not sent: ABP saves a background job in the same transaction as the
/// booking, so a booking that fails to save sends nothing, and a mail server that's down only
/// delays the email (ABP retries) — the booking itself is never held up by it.
///
/// Sent by <see cref="BookingEmailHandler"/> when the booking events are raised. Admin
/// cancellations are the one exception: they're gathered into one email per person, written
/// later by <see cref="AdminCancelledEmailJob"/> (see <see cref="AdminCancelEmailQueue"/>).
/// </summary>
public class BookingEmails : DomainService
{
    private readonly IIdentityUserRepository _userRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly UserLanguageManager _userLanguage;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly LocalizedNameReader _nameReader;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IEmailSender _emailSender;
    private readonly IStringLocalizer<DixelsResource> _localizer;
    private readonly IDataFilter _dataFilter;
    private readonly EmailOptions _options;

    public BookingEmails(
        IIdentityUserRepository userRepository,
        IBookingRepository bookingRepository,
        UserLanguageManager userLanguage,
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        LocalizedNameReader nameReader,
        ITemplateRenderer templateRenderer,
        IEmailSender emailSender,
        IStringLocalizer<DixelsResource> localizer,
        IDataFilter dataFilter,
        IOptions<EmailOptions> options)
    {
        _userRepository = userRepository;
        _bookingRepository = bookingRepository;
        _userLanguage = userLanguage;
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _nameReader = nameReader;
        _templateRenderer = templateRenderer;
        _emailSender = emailSender;
        _localizer = localizer;
        _dataFilter = dataFilter;
        _options = options.Value;
    }

    /// <summary>"You're booked" — for a booking the employee just made.</summary>
    public Task SendConfirmedAsync(Booking booking)
    {
        return SendAsync(booking.UserId, booking.SpaceId, DixelsEmailTemplates.BookingConfirmed, (model, clock) =>
        {
            Describe(model, clock, booking);
            model.Status = EmailStatus.Confirmed;
            model.Attendees = booking.Attendees;
            model.Heading = _localizer["Email:BookingConfirmed:Heading", TitleOrRoom(model)];
            return _localizer["Email:BookingConfirmed:Subject", model.SpaceName, model.Date];
        });
    }

    /// <summary>One email for a whole new series — not one per date.</summary>
    public Task SendSeriesConfirmedAsync(BookingSeries series, IReadOnlyCollection<Booking> bookings)
    {
        if (bookings.Count == 0)
        {
            return Task.CompletedTask;
        }

        return SendAsync(series.UserId, series.SpaceId, DixelsEmailTemplates.SeriesConfirmed, (model, clock) =>
        {
            var first = bookings.MinBy(b => b.StartsAt)!;
            Describe(model, clock, first);
            model.Status = EmailStatus.Confirmed;
            model.Title = series.Title;
            model.Attendees = series.Attendees;
            model.Count = bookings.Count;
            model.Heading = _localizer["Email:BookingConfirmed:Heading", TitleOrRoom(model)];

            // The When says how it repeats; the details, from when and which dates it skips.
            var rule = series.Rule;
            model.Rows[0].Date = RepeatText(rule, series.FirstDate);
            model.RepeatsFrom = _localizer["Email:RepeatsFrom", model.Date];
            var booked = bookings.Select(b => clock.LocalDate(b.StartsAt)).ToHashSet();
            var skipped = RecurrenceExpander.Expand(rule, series.FirstDate, BookingConsts.MaxSeriesOccurrences)
                .Where(d => !booked.Contains(d))
                .ToList();
            model.NotOn = skipped.Count == 0 ? null : Listed(skipped.Select(BookingFormat.Date), ShownSkippedDates);

            return _localizer["Email:SeriesConfirmed:Subject", model.SpaceName];
        });
    }

    /// <summary>"Your booking starts soon" — sent by <see cref="BookingReminders"/>.</summary>
    public Task SendReminderAsync(Booking booking)
    {
        return SendAsync(booking.UserId, booking.SpaceId, DixelsEmailTemplates.BookingReminder, (model, clock) =>
        {
            Describe(model, clock, booking);
            model.Status = EmailStatus.Reminder;
            model.Attendees = booking.Attendees;
            var startsAt = BookingFormat.Clock(TimeOnly.FromDateTime(clock.ToLocal(booking.StartsAt)));
            model.Heading = _localizer["Email:BookingReminder:Heading", TitleOrRoom(model), startsAt];
            return _localizer["Email:BookingReminder:Subject", model.SpaceName, startsAt];
        });
    }

    /// <summary>
    /// "You cancelled" — for what the employee just cancelled themselves: one booking, or
    /// several dates of one series, in one email. (Admin cancellations have their own.)
    /// </summary>
    public Task SendCancelledAsync(IReadOnlyCollection<Booking> cancelled)
    {
        if (cancelled.Count == 0)
        {
            return Task.CompletedTask;
        }

        var first = cancelled.MinBy(b => b.StartsAt)!;
        var last = cancelled.MaxBy(b => b.StartsAt)!;
        return SendAsync(first.UserId, first.SpaceId, DixelsEmailTemplates.BookingCancelled, (model, clock) =>
        {
            Describe(model, clock, first);
            model.Status = EmailStatus.Cancelled;
            model.Cancelled = true;
            model.Count = cancelled.Count;
            model.Reason = first.CancelReason;
            if (cancelled.Count > 1)
            {
                // Several dates of a series: the first to the last, at the series' time.
                model.Date = $"{model.Date} – {BookingFormat.Date(clock.LocalDate(last.StartsAt))}";
                model.Rows[0].Date = model.Date;
            }

            model.Heading = _localizer["Email:BookingCancelled:Heading", TitleOrRoom(model)];
            return cancelled.Count == 1
                ? _localizer["Email:BookingCancelled:Subject", model.SpaceName, model.Date]
                : _localizer["Email:SeriesCancelled:Subject", model.SpaceName];
        });
    }

    /// <summary>
    /// "An admin cancelled your bookings" — everything one admin action cancelled of this
    /// person's, in one email: each booking with its own room, time and reason, the soonest
    /// <paramref name="bookingIds"/> listed and the rest counted. Sent by
    /// <see cref="AdminCancelledEmailJob"/>, so it sends right away (and throws, for the job
    /// to retry). Nobody is emailed whose account is gone or switched off.
    /// </summary>
    public async Task SendAdminCancelledAsync(Guid userId, IReadOnlyCollection<Guid> bookingIds, int count)
    {
        var user = await _userRepository.FindAsync(userId);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var bookings = (await _bookingRepository.GetListAsync(b => bookingIds.Contains(b.Id) && b.UserId == userId))
            .OrderBy(b => b.StartsAt)
            .ThenBy(b => b.Id)
            .ToList();
        if (bookings.Count == 0)
        {
            return;
        }

        // A removed room (or its floor or building) is why many of these were cancelled: read
        // them anyway, for their names and time zone.
        List<Space> spaces;
        List<Floor> floors;
        List<Building> buildings;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var spaceIds = bookings.Select(b => b.SpaceId).Distinct().ToList();
            spaces = await AsyncExecuter.ToListAsync((await _spaceRepository.WithDetailsAsync()).Where(s => spaceIds.Contains(s.Id)));
            var floorIds = spaces.Select(s => s.FloorId).Distinct().ToList();
            floors = await AsyncExecuter.ToListAsync((await _floorRepository.WithDetailsAsync()).Where(f => floorIds.Contains(f.Id)));
            var buildingIds = floors.Select(f => f.BuildingId).Distinct().ToList();
            buildings = await AsyncExecuter.ToListAsync((await _buildingRepository.WithDetailsAsync()).Where(b => buildingIds.Contains(b.Id)));
        }

        var spaceById = spaces.ToDictionary(s => s.Id);
        var floorById = floors.ToDictionary(f => f.Id);
        var clocks = buildings.ToDictionary(b => b.Id, b => new BuildingClock(b.Timezone));

        var language = await _userLanguage.GetAsync(userId);
        using (CultureHelper.Use(language))
        {
            var spaceNames = await _nameReader.ShownAsync<Space, SpaceTranslation>(spaces);
            var floorNames = await _nameReader.ShownAsync<Floor, FloorTranslation>(floors);
            var buildingNames = await _nameReader.ShownAsync<Building, BuildingTranslation>(buildings);

            var model = new BookingEmailModel
            {
                Status = EmailStatus.Cancelled,
                Cancelled = true,
                RecipientName = DisplayName(user),
                FindUrl = AppLink("/find-space"),
                Count = Math.Max(count, bookings.Count),
            };
            foreach (var booking in bookings)
            {
                // Rooms are only ever soft-deleted; a missing one is skipped rather than failing the email.
                if (!spaceById.TryGetValue(booking.SpaceId, out var space)
                    || !floorById.TryGetValue(space.FloorId, out var floor)
                    || !clocks.TryGetValue(floor.BuildingId, out var clock))
                {
                    continue;
                }

                model.Rows.Add(new BookingEmailRow
                {
                    SpaceName = spaceNames[booking.SpaceId],
                    FloorName = floorNames[floor.Id],
                    BuildingName = buildingNames[floor.BuildingId],
                    Date = BookingFormat.Date(clock.LocalDate(booking.StartsAt)),
                    Time = TimeRange(clock.ToLocal(booking.StartsAt), clock.ToLocal(booking.EndsAt)),
                    Zone = ZoneLabel(clock, booking.StartsAt),
                    Title = booking.Title,
                    Reason = booking.CancelReason,
                });
            }
            if (model.Rows.Count == 0)
            {
                return;
            }

            model.More = model.Count - model.Rows.Count;

            var first = model.Rows[0];
            model.Reason = first.Reason;
            model.Heading = model.Count == 1
                ? _localizer["Email:AdminCancelled:Heading"]
                : _localizer["Email:AdminCancelled:HeadingMany", model.Count];
            var subject = model.Count == 1
                ? _localizer["Email:AdminCancelled:Subject", string.IsNullOrWhiteSpace(first.Title) ? first.SpaceName : first.Title, first.Date]
                : _localizer["Email:AdminCancelled:SubjectMany", model.Count];

            await _emailSender.SendAsync(user.Email, subject, await RenderAsync(DixelsEmailTemplates.AdminCancelled, model, language));
        }
    }

    /// <summary>
    /// Writes the email in the recipient's language and queues it. <paramref name="fill"/>
    /// adds the booking-specific parts (it runs in that language) and returns the subject.
    /// Never throws: an email that can't be written is logged, and the booking goes ahead.
    /// </summary>
    private async Task SendAsync(Guid userId, Guid spaceId, string template, Func<BookingEmailModel, BuildingClock, string> fill)
    {
        try
        {
            var user = await _userRepository.FindAsync(userId);
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var language = await _userLanguage.GetAsync(userId);
            using (CultureHelper.Use(language))
            {
                var space = await _spaceRepository.GetAsync(spaceId, includeDetails: true);
                var floor = await _floorRepository.GetAsync(space.FloorId, includeDetails: true);
                var building = await _buildingRepository.GetAsync(floor.BuildingId, includeDetails: true);

                var buildingNames = await _nameReader.ShownTranslationAsync(building);
                var model = new BookingEmailModel
                {
                    RecipientName = DisplayName(user),
                    SpaceName = await _nameReader.ShownAsync(space),
                    FloorName = await _nameReader.ShownAsync(floor),
                    BuildingName = buildingNames?.Name ?? string.Empty,
                    Address = buildingNames?.Address,
                    FindUrl = AppLink("/find-space"),
                };
                var subject = fill(model, new BuildingClock(building.Timezone));

                await _emailSender.QueueAsync(user.Email, subject, await RenderAsync(template, model, language));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not write the {Template} email to user {UserId}.", template, userId);
        }
    }

    /// <summary>
    /// The template in <paramref name="language"/> (the current culture), inside the layout.
    /// ABP hands the layout only the global values, so the model goes in there too: the
    /// layout draws the band, heading and When / Where blocks from it.
    /// </summary>
    private Task<string> RenderAsync(string template, BookingEmailModel model, string language)
    {
        return _templateRenderer.RenderAsync(template, model, language, new Dictionary<string, object>
        {
            ["lang"] = language,
            ["dir"] = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "rtl" : "ltr",
            ["model"] = model,
        });
    }

    private static string DisplayName(IdentityUser user)
    {
        var name = $"{user.Name} {user.Surname}".Trim();
        return name.Length > 0 ? name : user.UserName;
    }

    /// <summary>How many skipped dates a series email names before "+N more".</summary>
    private const int ShownSkippedDates = 5;

    /// <summary>
    /// The booking's When / Where block and the parts the subject and details use: its date,
    /// time and zone on the building's clock, its title, and "View booking" on its day.
    /// </summary>
    private void Describe(BookingEmailModel model, BuildingClock clock, Booking booking)
    {
        var date = clock.LocalDate(booking.StartsAt);
        model.Date = BookingFormat.Date(date);
        model.Title = booking.Title;
        model.Count = 1;
        model.ViewUrl = AppLink($"/my-calendar?view=day&date={date:yyyy-MM-dd}");
        model.Rows.Add(new BookingEmailRow
        {
            SpaceName = model.SpaceName,
            FloorName = model.FloorName,
            BuildingName = model.BuildingName,
            Date = model.Date,
            Time = TimeRange(clock.ToLocal(booking.StartsAt), clock.ToLocal(booking.EndsAt)),
            Zone = ZoneLabel(clock, booking.StartsAt),
        });
    }

    private static string TitleOrRoom(BookingEmailModel model) =>
        string.IsNullOrWhiteSpace(model.Title) ? model.SpaceName : model.Title;

    private string AppLink(string path) => _options.AppUrl.TrimEnd('/') + path;

    /// <summary>
    /// The building's zone, at that moment: "Amman time" (the city of its IANA id). Arabic
    /// shows the GMT offset instead ("GMT+3", the user's choice — no Arabic city names are at
    /// hand); worked out for that very instant, so summer and winter time both come out right.
    /// </summary>
    private string ZoneLabel(BuildingClock clock, DateTimeOffset at)
    {
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar")
        {
            var offset = clock.Zone.GetUtcOffset(at);
            if (offset == TimeSpan.Zero)
            {
                return "GMT";
            }

            var sign = offset < TimeSpan.Zero ? "-" : "+";
            var abs = offset.Duration();
            return abs.Minutes == 0 ? $"GMT{sign}{abs.Hours}" : $"GMT{sign}{abs.Hours}:{abs.Minutes:00}";
        }

        var id = clock.Zone.Id;
        if (id is "UTC" or "Etc/UTC")
        {
            return "UTC";
        }

        return _localizer["Email:ZoneTime", id[(id.LastIndexOf('/') + 1)..].Replace('_', ' ')];
    }

    /// <summary>
    /// How a series repeats, in words: "Every Monday until Mon 30 Nov 2026", "Every 2 weeks on
    /// Monday, Wednesday until …", "Monthly on the second Tuesday until …".
    /// </summary>
    private string RepeatText(RecurrenceRule rule, DateOnly first)
    {
        var until = BookingFormat.Date(rule.EndDate);
        var n = rule.Interval;
        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                return n == 1 ? _localizer["Email:Repeat:Daily", until] : _localizer["Email:Repeat:DailyN", n, until];

            case RecurrenceFrequency.Weekly:
                var days = string.Join(_localizer["Email:ListSeparator"], rule.Weekdays.Select(BookingFormat.Weekday));
                return n == 1 ? _localizer["Email:Repeat:Weekly", days, until] : _localizer["Email:Repeat:WeeklyN", n, days, until];

            case RecurrenceFrequency.Monthly when rule.MonthlyRepeat == MonthlyRepeat.OnWeekday:
                var position = _localizer[$"Email:Ordinal:{Math.Min(RecurrenceExpander.WeekOfMonth(first), 5)}"];
                var day = BookingFormat.Weekday(first.DayOfWeek);
                return n == 1
                    ? _localizer["Email:Repeat:MonthlyWeekday", position, day, until]
                    : _localizer["Email:Repeat:MonthlyWeekdayN", n, position, day, until];

            default:
                return n == 1
                    ? _localizer["Email:Repeat:MonthlyDay", first.Day, until]
                    : _localizer["Email:Repeat:MonthlyDayN", n, first.Day, until];
        }
    }

    /// <summary>The first few, joined, then "+N more".</summary>
    private string Listed(IEnumerable<string> items, int shown)
    {
        var all = items.ToList();
        var text = string.Join(_localizer["Email:ListSeparator"], all.Take(shown));
        return all.Count > shown ? _localizer["Email:AndMore", text, all.Count - shown] : text;
    }

    private static string TimeRange(DateTime start, DateTime end)
    {
        return $"{BookingFormat.Clock(TimeOnly.FromDateTime(start))}–{BookingFormat.Clock(TimeOnly.FromDateTime(end))}";
    }
}
