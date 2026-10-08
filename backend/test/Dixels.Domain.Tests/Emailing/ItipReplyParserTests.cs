using System;
using Dixels.Bookings;
using Dixels.Emails.Rsvp;
using Shouldly;
using Xunit;

namespace Dixels.Emailing;

/// <summary>Reading Accept / Decline replies from mail apps (see ItipReplyParser).</summary>
public class ItipReplyParserTests
{
    /// <summary>What Outlook sends back: its own Windows zone, the guest's address, ACCEPTED.</summary>
    private const string Outlook = """
        BEGIN:VCALENDAR
        METHOD:REPLY
        PRODID:Microsoft Exchange Server 2010
        VERSION:2.0
        BEGIN:VTIMEZONE
        TZID:Arab Standard Time
        BEGIN:STANDARD
        DTSTART:16010101T000000
        TZOFFSETFROM:+0300
        TZOFFSETTO:+0300
        END:STANDARD
        END:VTIMEZONE
        BEGIN:VEVENT
        ATTENDEE;PARTSTAT=ACCEPTED;CN=Omar Haddad:mailto:omar@outside.io
        SUMMARY;LANGUAGE=en-US:Accepted: Q4 planning
        DTSTART;TZID=Arab Standard Time:20261013T100000
        DTEND;TZID=Arab Standard Time:20261013T110000
        UID:Xp3abcQ@dixels
        CLASS:PUBLIC
        PRIORITY:5
        DTSTAMP:20261008T091500Z
        TRANSP:OPAQUE
        SEQUENCE:2
        LOCATION;LANGUAGE=en-US:Board room A
        END:VEVENT
        END:VCALENDAR
        """;

    /// <summary>What Gmail sends back: more attendee parameters, DECLINED.</summary>
    private const string Gmail = """
        BEGIN:VCALENDAR
        PRODID:-//Google Inc//Google Calendar 70.9054//EN
        VERSION:2.0
        CALSCALE:GREGORIAN
        METHOD:REPLY
        BEGIN:VEVENT
        DTSTART:20261013T070000Z
        DTEND:20261013T080000Z
        DTSTAMP:20261008T101010Z
        ORGANIZER;CN=Sara Ali (via Dixels):mailto:rsvp@dixels.local
        UID:Gm41lUid@dixels
        ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=DECLINED;CN=Omar
         Haddad;X-NUM-GUESTS=0:mailto:omar@gmail.com
        SEQUENCE:0
        STATUS:CONFIRMED
        SUMMARY:Q4 planning
        TRANSP:OPAQUE
        END:VEVENT
        END:VCALENDAR
        """;

    /// <summary>What Apple Calendar sends for "this event only" of a series: its RECURRENCE-ID, in our zone.</summary>
    private const string AppleOneDate = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//Apple Inc.//macOS 15.0//EN
        METHOD:REPLY
        BEGIN:VEVENT
        UID:Ser1esUid@dixels
        RECURRENCE-ID;TZID=Asia/Amman:20261020T090000
        DTSTART;TZID=Asia/Amman:20261020T090000
        DTEND;TZID=Asia/Amman:20261020T093000
        DTSTAMP:20261008T120000Z
        ATTENDEE;CN=Omar Haddad;PARTSTAT=ACCEPTED:mailto:omar@icloud.com
        SEQUENCE:1
        END:VEVENT
        END:VCALENDAR
        """;

    private static string With(string ics, string from, string to) => ics.ReplaceLineEndings("\n").Replace(from, to);

    [Fact]
    public void An_outlook_accept_is_read_by_its_uid()
    {
        var answer = ItipReplyParser.Parse(Outlook).ShouldHaveSingleItem();

        answer.Uid.ShouldBe("Xp3abcQ@dixels");
        answer.Status.ShouldBe(InviteeResponseStatus.Accepted);
        answer.Occurrence.ShouldBeNull();
        answer.AnsweredAtUtc.ShouldBe(new DateTime(2026, 10, 8, 9, 15, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void A_gmail_decline_is_read_whatever_address_it_names()
    {
        var answer = ItipReplyParser.Parse(Gmail).ShouldHaveSingleItem();

        answer.Uid.ShouldBe("Gm41lUid@dixels");
        answer.Status.ShouldBe(InviteeResponseStatus.Declined);
        answer.AnsweredAtUtc.ShouldBe(new DateTime(2026, 10, 8, 10, 10, 10, DateTimeKind.Utc));
    }

    [Fact]
    public void One_date_of_a_series_carries_its_original_start()
    {
        var answer = ItipReplyParser.Parse(AppleOneDate).ShouldHaveSingleItem();

        answer.Uid.ShouldBe("Ser1esUid@dixels");
        answer.Occurrence.ShouldBe(new DateTime(2026, 10, 20, 9, 0, 0));
        answer.OccurrenceIsUtc.ShouldBeFalse();

        var inUtc = ItipReplyParser.Parse(With(AppleOneDate, "RECURRENCE-ID;TZID=Asia/Amman:20261020T090000", "RECURRENCE-ID:20261020T060000Z"))
            .ShouldHaveSingleItem();
        inUtc.Occurrence.ShouldBe(new DateTime(2026, 10, 20, 6, 0, 0));
        inUtc.OccurrenceIsUtc.ShouldBeTrue();
    }

    [Theory]
    [InlineData("PARTSTAT=TENTATIVE")]      // "Maybe" is no answer
    [InlineData("PARTSTAT=NEEDS-ACTION")]
    [InlineData("PARTSTAT=DELEGATED")]
    public void Anything_but_accept_or_decline_is_no_answer(string partstat)
    {
        ItipReplyParser.Parse(With(Outlook, "PARTSTAT=ACCEPTED", partstat)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("METHOD:COUNTER")]          // "Propose new time"
    [InlineData("METHOD:REQUEST")]          // our own invite, caught by smtp4dev too
    [InlineData("METHOD:PUBLISH")]
    [InlineData("METHOD:CANCEL")]
    public void Only_a_reply_counts(string method)
    {
        ItipReplyParser.Parse(With(Outlook, "METHOD:REPLY", method)).ShouldBeEmpty();
    }

    [Fact]
    public void A_calendar_without_method_counts_when_its_mail_part_says_reply()
    {
        var bare = With(Outlook, "METHOD:REPLY\n", "");

        ItipReplyParser.Parse(bare).ShouldBeEmpty();
        ItipReplyParser.Parse(bare, mimeMethod: "REPLY").ShouldHaveSingleItem().Status.ShouldBe(InviteeResponseStatus.Accepted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Hi, yes I'll be there!")]
    [InlineData("BEGIN:VCALENDAR\nMETHOD:REPLY\nBEGIN:VEVENT\nUID:x")]
    public void Junk_is_no_answer(string? calendar)
    {
        ItipReplyParser.Parse(calendar).ShouldBeEmpty();
    }

    [Fact]
    public void A_reply_without_one_of_our_uids_or_an_attendee_is_no_answer()
    {
        ItipReplyParser.Parse(With(Outlook, "UID:Xp3abcQ@dixels\n", "")).ShouldBeEmpty();
        ItipReplyParser.Parse(With(Outlook, "UID:Xp3abcQ@dixels", "UID:040000008200E00074C5B7101A82E008")).ShouldBeEmpty();
        ItipReplyParser.Parse(With(Outlook, "ATTENDEE;PARTSTAT=ACCEPTED;CN=Omar Haddad:mailto:omar@outside.io\n", "")).ShouldBeEmpty();
    }

    [Fact]
    public void An_oversized_calendar_is_not_read()
    {
        var huge = Outlook.Replace("CLASS:PUBLIC", "X-PAD:" + new string('a', ItipReplyParser.MaxLength));

        ItipReplyParser.Parse(huge).ShouldBeEmpty();
    }
}
