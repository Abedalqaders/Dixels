using System.Collections.Generic;

namespace Dixels.Bookings;

// What happens to bookings, announced on ABP's local event bus (ILocalEventHandler<T> to
// listen; BookingEmailHandler is one). Each event says how it happened too — a retried
// request raises nothing, a series is one event, a cancel says who did it — so a listener
// never has to guess.
//
// Listeners run inside the booking's own transaction, when it saves. So a listener must:
// - be quick: queue slow work (an email, a Teams message) as a background job, never call
//   another server right there — the booking waits for it;
// - never throw: an exception rolls the booking back with it.

/// <summary>A new booking was made (not a retry of one already made).</summary>
public class BookingConfirmedEvent
{
    public Booking Booking { get; }

    public BookingConfirmedEvent(Booking booking)
    {
        Booking = booking;
    }
}

/// <summary>A new series was made: one event for all its dates.</summary>
public class BookingSeriesConfirmedEvent
{
    public BookingSeries Series { get; }
    public IReadOnlyList<Booking> Bookings { get; }

    public BookingSeriesConfirmedEvent(BookingSeries series, IReadOnlyList<Booking> bookings)
    {
        Series = series;
        Bookings = bookings;
    }
}

/// <summary>
/// Bookings were cancelled together: an employee cancelling their own (one, or several dates
/// of a series), or an admin's change (rules, a closure, a removed room) cancelling bookings
/// that no longer fit — possibly several people's.
/// </summary>
public class BookingsCancelledEvent
{
    public IReadOnlyList<Booking> Bookings { get; }
    public bool ByAdmin { get; }

    public BookingsCancelledEvent(IReadOnlyList<Booking> bookings, bool byAdmin)
    {
        Bookings = bookings;
        ByAdmin = byAdmin;
    }
}

/// <summary>A booking starts soon and is due its one reminder (see BookingReminders).</summary>
public class BookingReminderDueEvent
{
    public Booking Booking { get; }

    public BookingReminderDueEvent(Booking booking)
    {
        Booking = booking;
    }
}
