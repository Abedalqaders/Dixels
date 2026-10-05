using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace Dixels.Bookings;

/// <summary>
/// "Your booking starts soon" emails, <see cref="BookingOptions.ReminderLeadMinutes"/> before
/// each confirmed booking. <see cref="BookingReminderWorker"/> calls <see cref="SendDueAsync"/>
/// every minute.
///
/// Each reminder is its own transaction: the booking is stamped
/// (<see cref="Booking.ReminderSentAt"/>) and <see cref="BookingReminderDueEvent"/> raised in
/// it — its listeners (the email) queue their work there too — so it's sent once —
/// a second app instance doing the same at the same moment fails on the booking's
/// concurrency stamp and rolls back its copy.
/// </summary>
public class BookingReminders : DomainService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ILocalEventBus _localEventBus;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly BookingOptions _options;

    public BookingReminders(
        IBookingRepository bookingRepository,
        ILocalEventBus localEventBus,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<BookingOptions> options)
    {
        _bookingRepository = bookingRepository;
        _localEventBus = localEventBus;
        _unitOfWorkManager = unitOfWorkManager;
        _options = options.Value;
    }

    /// <summary>Sends every reminder that's due now; returns how many.</summary>
    public async Task<int> SendDueAsync()
    {
        if (_options.ReminderLeadMinutes <= 0)
        {
            return 0;
        }

        var lead = TimeSpan.FromMinutes(_options.ReminderLeadMinutes);
        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);

        // Only bookings that haven't started: once under way "starts soon" is too late, even
        // if the app was down when the reminder was due.
        List<Booking> due;
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true))
        {
            due = await _bookingRepository.GetDueForReminderAsync(now, now + lead);
            await uow.CompleteAsync();
        }

        var sent = 0;
        foreach (var candidate in due)
        {
            // Booked inside the window: the confirmation went out minutes ago, a reminder
            // on top of it would only be noise.
            if (candidate.CreationTime > (candidate.StartsAt - lead).UtcDateTime)
            {
                continue;
            }

            try
            {
                using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
                var booking = await _bookingRepository.GetAsync(candidate.Id);
                if (booking.Status != BookingStatus.Confirmed || booking.ReminderSentAt is not null)
                {
                    continue;
                }

                booking.MarkReminderSent(now);
                await _bookingRepository.UpdateAsync(booking);
                await _localEventBus.PublishAsync(new BookingReminderDueEvent(booking));
                await uow.CompleteAsync();
                sent++;
            }
            catch (Exception ex)
            {
                // Most likely another instance got there first. Either way the next minute
                // looks again, and one booking never holds up the rest.
                Logger.LogWarning(ex, "Could not send the reminder for booking {BookingId}.", candidate.Id);
            }
        }

        return sent;
    }
}
