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
/// Sent by <see cref="BookingEmailHandler"/> when the booking events are raised; admin
/// cancellations deliberately send nothing (the employee sees them in their calendar).
/// </summary>
public class BookingEmails : DomainService
{
    private readonly IIdentityUserRepository _userRepository;
    private readonly UserLanguageManager _userLanguage;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly LocalizedNameReader _nameReader;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IEmailSender _emailSender;
    private readonly IStringLocalizer<DixelsResource> _localizer;
    private readonly EmailOptions _options;

    public BookingEmails(
        IIdentityUserRepository userRepository,
        UserLanguageManager userLanguage,
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        LocalizedNameReader nameReader,
        ITemplateRenderer templateRenderer,
        IEmailSender emailSender,
        IStringLocalizer<DixelsResource> localizer,
        IOptions<EmailOptions> options)
    {
        _userRepository = userRepository;
        _userLanguage = userLanguage;
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _nameReader = nameReader;
        _templateRenderer = templateRenderer;
        _emailSender = emailSender;
        _localizer = localizer;
        _options = options.Value;
    }

    /// <summary>"Your booking is confirmed" — for a booking the employee just made.</summary>
    public Task SendConfirmedAsync(Booking booking)
    {
        return SendAsync(booking.UserId, booking.SpaceId, DixelsEmailTemplates.BookingConfirmed, (model, clock) =>
        {
            DescribeDates(model, clock, new[] { booking });
            model.Attendees = booking.Attendees;
            model.Title = booking.Title;
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
            DescribeDates(model, clock, bookings);
            model.Attendees = series.Attendees;
            model.Title = series.Title;
            return _localizer["Email:SeriesConfirmed:Subject", model.SpaceName];
        });
    }

    /// <summary>"Your booking starts soon" — sent by <see cref="BookingReminders"/>.</summary>
    public Task SendReminderAsync(Booking booking)
    {
        return SendAsync(booking.UserId, booking.SpaceId, DixelsEmailTemplates.BookingReminder, (model, clock) =>
        {
            DescribeDates(model, clock, new[] { booking });
            model.Attendees = booking.Attendees;
            model.Title = booking.Title;
            var startsAt = BookingFormat.Clock(TimeOnly.FromDateTime(clock.ToLocal(booking.StartsAt)));
            return _localizer["Email:BookingReminder:Subject", model.SpaceName, startsAt];
        });
    }

    /// <summary>
    /// "Your booking is cancelled" — for what the employee just cancelled themselves: one
    /// booking, or several dates of one series, in one email. (Not for admin cancellations.)
    /// </summary>
    public Task SendCancelledAsync(IReadOnlyCollection<Booking> cancelled)
    {
        if (cancelled.Count == 0)
        {
            return Task.CompletedTask;
        }

        var first = cancelled.MinBy(b => b.StartsAt)!;
        return SendAsync(first.UserId, first.SpaceId, DixelsEmailTemplates.BookingCancelled, (model, clock) =>
        {
            DescribeDates(model, clock, cancelled);
            model.Attendees = first.Attendees;
            model.Title = first.Title;
            model.Reason = first.CancelReason;
            return cancelled.Count == 1
                ? _localizer["Email:BookingCancelled:Subject", model.SpaceName, model.Date]
                : _localizer["Email:SeriesCancelled:Subject", model.SpaceName];
        });
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

                var model = new BookingEmailModel
                {
                    RecipientName = DisplayName(user),
                    SpaceName = await _nameReader.ShownAsync(space),
                    FloorName = await _nameReader.ShownAsync(floor),
                    BuildingName = await _nameReader.ShownAsync(building),
                    AppUrl = _options.AppUrl.TrimEnd('/') + "/my-calendar",
                };
                var subject = fill(model, new BuildingClock(building.Timezone));

                var body = await _templateRenderer.RenderAsync(template, model, language, new Dictionary<string, object>
                {
                    ["lang"] = language,
                    ["dir"] = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "rtl" : "ltr",
                });

                await _emailSender.QueueAsync(user.Email, subject, body);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not write the {Template} email to user {UserId}.", template, userId);
        }
    }

    private static string DisplayName(IdentityUser user)
    {
        var name = $"{user.Name} {user.Surname}".Trim();
        return name.Length > 0 ? name : user.UserName;
    }

    /// <summary>
    /// One booking: its date and time. Several (a series): the first and last date, the
    /// first one's time — every date of a series is at the same local time — and how many.
    /// </summary>
    private static void DescribeDates(BookingEmailModel model, BuildingClock clock, IReadOnlyCollection<Booking> bookings)
    {
        var first = bookings.MinBy(b => b.StartsAt)!;
        var last = bookings.MaxBy(b => b.StartsAt)!;
        model.Date = bookings.Count == 1
            ? BookingFormat.Date(clock.LocalDate(first.StartsAt))
            : $"{BookingFormat.Date(clock.LocalDate(first.StartsAt))} – {BookingFormat.Date(clock.LocalDate(last.StartsAt))}";
        model.Time = TimeRange(clock.ToLocal(first.StartsAt), clock.ToLocal(first.EndsAt));
        model.Count = bookings.Count;
    }

    private static string TimeRange(DateTime start, DateTime end)
    {
        return $"{BookingFormat.Clock(TimeOnly.FromDateTime(start))}–{BookingFormat.Clock(TimeOnly.FromDateTime(end))}";
    }
}
