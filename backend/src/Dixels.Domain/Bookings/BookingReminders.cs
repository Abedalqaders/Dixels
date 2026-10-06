using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace Dixels.Bookings;

/// <summary>
/// "Your booking starts soon" emails, <see cref="BookingOptions.ReminderLeadMinutes"/> before
/// each confirmed booking. <see cref="BookingReminderWorker"/> calls <see cref="SendDueAsync"/>
/// every minute.
///
/// Reminders go out <see cref="BatchSize"/> at a time, one transaction per batch: the bookings
/// are stamped (<see cref="Booking.ReminderSentAt"/>) and a <see cref="BookingReminderDueEvent"/>
/// raised for each — its listeners (the email) queue their work in the same transaction — so
/// each is sent once. If a batch fails it is retried one booking per transaction, so one bad
/// booking is skipped (and logged) instead of holding up the rest.
///
/// With several app servers only the one holding the distributed lock runs; the others skip
/// that minute. Should two ever overlap anyway (the lock lost with its connection), the second
/// fails on the booking's concurrency stamp and rolls back its copy.
/// </summary>
public class BookingReminders : DomainService
{
    public const string LockName = "Dixels:BookingReminders";
    public const int BatchSize = 200;

    private readonly IBookingRepository _bookingRepository;
    private readonly ILocalEventBus _localEventBus;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly BookingOptions _options;

    public BookingReminders(
        IBookingRepository bookingRepository,
        ILocalEventBus localEventBus,
        IUnitOfWorkManager unitOfWorkManager,
        IAbpDistributedLock distributedLock,
        IOptions<BookingOptions> options)
    {
        _bookingRepository = bookingRepository;
        _localEventBus = localEventBus;
        _unitOfWorkManager = unitOfWorkManager;
        _distributedLock = distributedLock;
        _options = options.Value;
    }

    /// <summary>
    /// Sends every reminder that's due now; returns how many (0 when another server is
    /// already doing it).
    /// </summary>
    public async Task<int> SendDueAsync()
    {
        if (_options.ReminderLeadMinutes <= 0)
        {
            return 0;
        }

        await using var handle = await _distributedLock.TryAcquireAsync(LockName);
        if (handle is null)
        {
            return 0;
        }

        var lead = TimeSpan.FromMinutes(_options.ReminderLeadMinutes);
        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);

        // Bookings this run leaves unstamped (booked inside the window, or failing): the next
        // batch skips them, so every batch moves on.
        var passedOver = new HashSet<Guid>();
        var sent = 0;
        while (true)
        {
            List<Booking> batch;
            var stamped = new List<Guid>();
            try
            {
                using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
                // Only bookings that haven't started: once under way "starts soon" is too late,
                // even if the app was down when the reminder was due.
                batch = await _bookingRepository.GetDueForReminderAsync(now, now + lead, BatchSize, passedOver);
                foreach (var booking in batch)
                {
                    // Booked inside the window: the confirmation went out minutes ago, a
                    // reminder on top of it would only be noise.
                    if (booking.CreationTime > (booking.StartsAt - lead).UtcDateTime)
                    {
                        passedOver.Add(booking.Id);
                        continue;
                    }

                    booking.MarkReminderSent(now);
                    await _localEventBus.PublishAsync(new BookingReminderDueEvent(booking));
                    stamped.Add(booking.Id);
                }

                await uow.CompleteAsync();
                sent += stamped.Count;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "A batch of {Count} booking reminders failed; sending them one by one.", stamped.Count);
                foreach (var id in stamped)
                {
                    if (await SendOneAsync(id, now))
                    {
                        sent++;
                    }
                    else
                    {
                        passedOver.Add(id);
                    }
                }

                // The query itself failed: nothing to retry, the next minute looks again.
                if (stamped.Count == 0)
                {
                    break;
                }

                continue;
            }

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        return sent;
    }

    /// <summary>The reminder for one booking in its own transaction; false if it failed.</summary>
    private async Task<bool> SendOneAsync(Guid bookingId, DateTimeOffset now)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var booking = await _bookingRepository.GetAsync(bookingId);
            if (booking.Status != BookingStatus.Confirmed || booking.ReminderSentAt is not null)
            {
                return false;
            }

            booking.MarkReminderSent(now);
            await _bookingRepository.UpdateAsync(booking);
            await _localEventBus.PublishAsync(new BookingReminderDueEvent(booking));
            await uow.CompleteAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not send the reminder for booking {BookingId}.", bookingId);
            return false;
        }
    }
}
