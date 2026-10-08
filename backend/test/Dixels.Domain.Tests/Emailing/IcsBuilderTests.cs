using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.Bookings;
using Dixels.Emails;
using Ical.Net;
using Shouldly;
using Xunit;

namespace Dixels.Emailing;

/// <summary>The calendar files the emails carry (see IcsBuilder).</summary>
public class IcsBuilderTests
{
    private static readonly DateTime Stamp = new(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

    private static IcsEvent Single(string method = IcsMethods.Publish) => new()
    {
        Uid = "abc123@dixels",
        Sequence = 2,
        Method = method,
        Summary = "Q4 planning",
        Location = "Board room A · Level 3 · Riverside HQ · 12 King Hussein St, Amman",
        TimeZoneId = "Asia/Amman",
        LocalStart = new DateTime(2026, 10, 13, 10, 0, 0),
        LocalEnd = new DateTime(2026, 10, 13, 11, 0, 0),
        Organizer = new IcsPerson("Sara Ali (via Dixels)", "rsvp@dixels.local"),
        Attendee = new IcsPerson("Omar Haddad", "omar@test.io"),
        StampUtc = Stamp,
    };

    [Fact]
    public void A_booking_is_one_event_in_the_buildings_zone()
    {
        var ics = Build(Single());

        ics.ShouldContain("METHOD:PUBLISH");
        ics.ShouldContain("BEGIN:VTIMEZONE");
        ics.ShouldContain("TZID:Asia/Amman");
        ics.ShouldContain("DTSTART;TZID=Asia/Amman:20261013T100000");
        ics.ShouldContain("DTEND;TZID=Asia/Amman:20261013T110000");
        ics.ShouldContain("UID:abc123@dixels");
        ics.ShouldContain("SEQUENCE:2");
        ics.ShouldContain("SUMMARY:Q4 planning");
        ics.ShouldContain("mailto:rsvp@dixels.local");

        // A calendar reads it back as 10:00 Amman time = 07:00 UTC.
        var evt = Calendar.Load(ics)!.Events.ShouldHaveSingleItem();
        evt.Uid.ShouldBe("abc123@dixels");
        evt.RecurrenceRules.ShouldBeEmpty();
        evt.DtStart!.AsUtc.ShouldBe(new DateTime(2026, 10, 13, 7, 0, 0, DateTimeKind.Utc));
        evt.Attendees.ShouldHaveSingleItem().Value!.ToString().ShouldBe("mailto:omar@test.io");
    }

    [Fact]
    public void Only_a_request_asks_the_guest_to_answer()
    {
        Build(Single()).ShouldNotContain("RSVP=TRUE");
        Build(Single(IcsMethods.Request) with { AskToAnswer = true }).ShouldContain("RSVP=TRUE");
    }

    [Fact]
    public void A_cancel_keeps_the_uid_and_says_cancelled()
    {
        var ics = Build(Single(IcsMethods.Cancel) with { Sequence = 3 });

        ics.ShouldContain("METHOD:CANCEL");
        ics.ShouldContain("UID:abc123@dixels");
        ics.ShouldContain("SEQUENCE:3");
        ics.ShouldContain("STATUS:CANCELLED");
    }

    public static IEnumerable<object[]> Rules() => new[]
    {
        new object[] { "daily", new RecurrenceRule(RecurrenceFrequency.Daily, 1, null, MonthlyRepeat.OnDay, new DateOnly(2026, 10, 20)), new DateOnly(2026, 10, 12) },
        new object[] { "every 3 days", new RecurrenceRule(RecurrenceFrequency.Daily, 3, null, MonthlyRepeat.OnDay, new DateOnly(2026, 11, 20)), new DateOnly(2026, 10, 12) },
        new object[] { "weekly Mon+Wed", new RecurrenceRule(RecurrenceFrequency.Weekly, 1, new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, MonthlyRepeat.OnDay, new DateOnly(2026, 11, 30)), new DateOnly(2026, 10, 12) },
        new object[] { "every 2 weeks Sun+Sat, starting midweek", new RecurrenceRule(RecurrenceFrequency.Weekly, 2, new[] { DayOfWeek.Sunday, DayOfWeek.Saturday }, MonthlyRepeat.OnDay, new DateOnly(2026, 12, 31)), new DateOnly(2026, 10, 17) },
        new object[] { "monthly on day 15", new RecurrenceRule(RecurrenceFrequency.Monthly, 1, null, MonthlyRepeat.OnDay, new DateOnly(2027, 6, 30)), new DateOnly(2026, 10, 15) },
        new object[] { "monthly on day 30 (short months: their last day)", new RecurrenceRule(RecurrenceFrequency.Monthly, 1, null, MonthlyRepeat.OnDay, new DateOnly(2027, 9, 30)), new DateOnly(2026, 10, 30) },
        new object[] { "monthly on day 31", new RecurrenceRule(RecurrenceFrequency.Monthly, 1, null, MonthlyRepeat.OnDay, new DateOnly(2027, 9, 30)), new DateOnly(2026, 10, 31) },
        new object[] { "every 2 months on the 2nd Tuesday", new RecurrenceRule(RecurrenceFrequency.Monthly, 2, null, MonthlyRepeat.OnWeekday, new DateOnly(2027, 10, 31)), new DateOnly(2026, 10, 13) },
        new object[] { "monthly on the last Thursday", new RecurrenceRule(RecurrenceFrequency.Monthly, 1, null, MonthlyRepeat.OnWeekday, new DateOnly(2027, 10, 31)), new DateOnly(2026, 10, 29) },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void A_series_rule_lands_on_exactly_the_dates_dixels_books(string name, RecurrenceRule rule, DateOnly first)
    {
        var expected = RecurrenceExpander.Expand(rule, first, 500);
        var ics = Build(Single() with
        {
            LocalStart = first.ToDateTime(new TimeOnly(10, 0)),
            LocalEnd = first.ToDateTime(new TimeOnly(11, 0)),
            Rule = rule,
        });

        Dates(ics).ShouldBe(expected, name);
    }

    [Fact]
    public void Dates_that_werent_booked_are_left_out()
    {
        var rule = new RecurrenceRule(RecurrenceFrequency.Weekly, 1, new[] { DayOfWeek.Monday }, MonthlyRepeat.OnDay, new DateOnly(2026, 11, 2));
        var ics = Build(Single() with
        {
            LocalStart = new DateTime(2026, 10, 12, 9, 0, 0),
            LocalEnd = new DateTime(2026, 10, 12, 9, 30, 0),
            Rule = rule,
            LocalExceptions = new[] { new DateTime(2026, 10, 26, 9, 0, 0) },
        });

        ics.ShouldContain("RRULE:FREQ=WEEKLY");
        ics.ShouldContain("EXDATE;TZID=Asia/Amman:20261026T090000");
        Dates(ics).ShouldBe(new[] { new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19), new DateOnly(2026, 11, 2) });
    }

    [Fact]
    public void A_series_keeps_its_wall_clock_time_across_a_clock_change()
    {
        // London goes back an hour on 25 October 2026: 10:00 stays 10:00 local.
        var rule = new RecurrenceRule(RecurrenceFrequency.Weekly, 1, new[] { DayOfWeek.Friday }, MonthlyRepeat.OnDay, new DateOnly(2026, 10, 30));
        var ics = Build(Single() with
        {
            TimeZoneId = "Europe/London",
            LocalStart = new DateTime(2026, 10, 23, 10, 0, 0),
            LocalEnd = new DateTime(2026, 10, 23, 11, 0, 0),
            Rule = rule,
        });

        var starts = Calendar.Load(ics)!.Events.Single().GetOccurrences().Select(o => o.Period.StartTime.AsUtc).ToList();
        starts.ShouldBe(new[]
        {
            new DateTime(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc),
        });
    }

    [Fact]
    public void One_date_of_a_series_is_named_by_its_recurrence_id()
    {
        var ics = Build(Single(IcsMethods.Cancel) with { LocalRecurrenceIds = new[] { new DateTime(2026, 10, 19, 9, 0, 0) } });

        ics.ShouldContain("RECURRENCE-ID;TZID=Asia/Amman:20261019T090000");
        ics.ShouldContain("DTSTART;TZID=Asia/Amman:20261019T090000");
        ics.ShouldContain("DTEND;TZID=Asia/Amman:20261019T100000"); // the series' length
    }

    [Fact]
    public void Several_dates_of_a_series_are_one_event_each_with_the_same_uid()
    {
        var ics = Build(Single(IcsMethods.Cancel) with
        {
            LocalRecurrenceIds = new[] { new DateTime(2026, 10, 19, 9, 0, 0), new DateTime(2026, 10, 26, 9, 0, 0) },
        });

        var events = Calendar.Load(ics)!.Events;
        events.Count.ShouldBe(2);
        events.ShouldAllBe(evt => evt.Uid == "abc123@dixels" && evt.Status == "CANCELLED");
        ics.ShouldContain("RECURRENCE-ID;TZID=Asia/Amman:20261026T090000");
        ics.ShouldNotContain("RRULE:FREQ=WEEKLY");
    }

    /// <summary>The file with its long lines unfolded (RFC 5545 wraps them at 75 characters).</summary>
    private static string Build(IcsEvent e) => IcsBuilder.Build(e).Replace("\r\n ", "");

    private static List<DateOnly> Dates(string ics) =>
        Calendar.Load(ics)!.Events.Single().GetOccurrences()
            .Select(o => DateOnly.FromDateTime(o.Period.StartTime.Value))
            .ToList();
}
