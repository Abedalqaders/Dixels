using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.Bookings;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using RecurrenceRule = Dixels.Bookings.RecurrenceRule;

namespace Dixels.Emails;

/// <summary>The iTIP method of a calendar file: what the mail app does with it.</summary>
public static class IcsMethods
{
    /// <summary>"Add to calendar", no answer expected.</summary>
    public const string Publish = "PUBLISH";

    /// <summary>A meeting invite the mail app answers itself (E6).</summary>
    public const string Request = "REQUEST";

    /// <summary>Takes the event (or one date of it) off the calendar.</summary>
    public const string Cancel = "CANCEL";
}

public record IcsPerson(string Name, string Email);

/// <summary>
/// One calendar event, as one person gets it: a booking, or a whole series (one event with a
/// repeat rule). Times are building-local wall-clock times in <see cref="TimeZoneId"/>; the
/// file carries that zone, so each calendar shows them in its owner's own time.
/// </summary>
public record IcsEvent
{
    /// <summary>The same for every update and cancel of this person's copy, so they replace it.</summary>
    public string Uid { get; init; } = string.Empty;

    /// <summary>Goes up on each update or cancel of this copy.</summary>
    public int Sequence { get; init; }

    public string Method { get; init; } = IcsMethods.Publish;
    public string Summary { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>The building's IANA zone, e.g. "Asia/Amman".</summary>
    public string TimeZoneId { get; init; } = "UTC";

    /// <summary>The (first) start and its end, building-local.</summary>
    public DateTime LocalStart { get; init; }
    public DateTime LocalEnd { get; init; }

    /// <summary>A series: how it repeats. Null for a single booking.</summary>
    public RecurrenceRule? Rule { get; init; }

    /// <summary>A series: the dates the rule lands on that aren't booked (building-local starts).</summary>
    public IReadOnlyList<DateTime> LocalExceptions { get; init; } = Array.Empty<DateTime>();

    /// <summary>One date of a series this file is about (its original local start), e.g. cancelling just that date.</summary>
    public DateTime? LocalRecurrenceId { get; init; }

    public IcsPerson? Organizer { get; init; }

    /// <summary>Who this copy is for; never the other guests (names and emails stay private).</summary>
    public IcsPerson? Attendee { get; init; }

    /// <summary>Whether the attendee is asked to answer (a REQUEST).</summary>
    public bool AskToAnswer { get; init; }

    /// <summary>When the file was written (DTSTAMP), UTC.</summary>
    public DateTime StampUtc { get; init; }
}

/// <summary>
/// Writes <see cref="IcsEvent"/>s as iCalendar files (RFC 5545) with Ical.Net: the event in
/// the building's zone (with its VTIMEZONE), a series as one event with an RRULE that lands
/// on exactly the dates <see cref="RecurrenceExpander"/> does, plus EXDATEs for the dates
/// that weren't booked.
/// </summary>
public static class IcsBuilder
{
    public static string Build(IcsEvent e)
    {
        var calendar = new Calendar { Method = e.Method };
        calendar.ProductId = "-//Dixels//Bookings//EN";
        calendar.AddTimeZone(VTimeZone.FromDateTimeZone(e.TimeZoneId));

        var evt = new CalendarEvent
        {
            Uid = e.Uid,
            Sequence = e.Sequence,
            Summary = e.Summary,
            Location = e.Location,
            Description = e.Description,
            DtStart = Local(e.LocalStart, e.TimeZoneId),
            DtEnd = Local(e.LocalEnd, e.TimeZoneId),
            DtStamp = new CalDateTime(DateTime.SpecifyKind(e.StampUtc, DateTimeKind.Utc)),
            Status = e.Method == IcsMethods.Cancel ? EventStatus.Cancelled : EventStatus.Confirmed,
            Transparency = TransparencyType.Opaque,
        };

        if (e.Rule is not null)
        {
            evt.RecurrenceRules.Add(Pattern(e.Rule, e.LocalStart, e.TimeZoneId));
            foreach (var date in e.LocalExceptions)
            {
                evt.ExceptionDates.Add(Local(date, e.TimeZoneId));
            }
        }

        if (e.LocalRecurrenceId is { } recurrenceId)
        {
            evt.RecurrenceId = Local(recurrenceId, e.TimeZoneId);
        }

        if (e.Organizer is { } organizer)
        {
            evt.Organizer = new Organizer("mailto:" + organizer.Email) { CommonName = organizer.Name };
        }

        if (e.Attendee is { } attendee)
        {
            evt.Attendees.Add(new Attendee("mailto:" + attendee.Email)
            {
                CommonName = attendee.Name,
                Role = ParticipationRole.RequiredParticipant,
                ParticipationStatus = EventParticipationStatus.NeedsAction,
                Rsvp = e.AskToAnswer,
            });
        }

        calendar.Events.Add(evt);
        return new CalendarSerializer().SerializeToString(calendar)!;
    }

    private static CalDateTime Local(DateTime local, string timeZoneId) =>
        new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZoneId);

    /// <summary>
    /// The RRULE for a Dixels rule. Weeks start on Sunday, as in the expander. "Monthly on day
    /// 29–30" falls back to a short month's last day (the first of "that day or the last");
    /// day 31 is simply the last. A date in a month's 5th week repeats as "the last" weekday.
    /// UNTIL is the last date at the booking's start time, in UTC as RFC 5545 requires.
    /// </summary>
    private static RecurrencePattern Pattern(RecurrenceRule rule, DateTime localStart, string timeZoneId)
    {
        var first = DateOnly.FromDateTime(localStart);
        var clock = new BuildingClock(timeZoneId);
        var pattern = new RecurrencePattern
        {
            Interval = rule.Interval,
            FirstDayOfWeek = DayOfWeek.Sunday,
            Until = new CalDateTime(clock.ToUtc(rule.EndDate.ToDateTime(TimeOnly.FromDateTime(localStart))).UtcDateTime),
        };

        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                pattern.Frequency = FrequencyType.Daily;
                break;

            case RecurrenceFrequency.Weekly:
                pattern.Frequency = FrequencyType.Weekly;
                pattern.ByDay = rule.Weekdays.Select(d => new WeekDay(d)).ToList();
                break;

            case RecurrenceFrequency.Monthly when rule.MonthlyRepeat == MonthlyRepeat.OnWeekday:
                pattern.Frequency = FrequencyType.Monthly;
                var position = RecurrenceExpander.WeekOfMonth(first);
                pattern.ByDay = new List<WeekDay> { new(first.DayOfWeek, position <= 4 ? position : -1) };
                break;

            case RecurrenceFrequency.Monthly:
                pattern.Frequency = FrequencyType.Monthly;
                if (first.Day <= 28)
                {
                    pattern.ByMonthDay = new List<int> { first.Day };
                }
                else if (first.Day == 31)
                {
                    pattern.ByMonthDay = new List<int> { -1 };
                }
                else
                {
                    pattern.ByMonthDay = new List<int> { first.Day, -1 };
                    pattern.BySetPosition = new List<int> { 1 };
                }

                break;
        }

        return pattern;
    }
}
