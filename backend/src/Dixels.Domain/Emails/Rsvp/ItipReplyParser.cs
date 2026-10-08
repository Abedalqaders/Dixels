using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.Bookings;
using Ical.Net;

namespace Dixels.Emails.Rsvp;

/// <summary>
/// One answer in a guest's mail-app reply: which copy of the invite (<see cref="Uid"/>, the
/// guest's own secret one), optionally which date of a series, and what they said.
/// </summary>
/// <param name="Occurrence">
/// A series: the one date answered ("this occurrence only"), as the RECURRENCE-ID the mail app
/// echoed back — the original start, in the building's zone (as we sent it) unless
/// <see cref="OccurrenceIsUtc"/>. Null: the whole event.
/// </param>
/// <param name="AnsweredAtUtc">The reply's DTSTAMP: when the guest answered, not when we read it.</param>
public sealed record ItipAnswer(
    string Uid,
    DateTime? Occurrence,
    bool OccurrenceIsUtc,
    InviteeResponseStatus Status,
    DateTime AnsweredAtUtc);

/// <summary>
/// Reads the answers out of an iTIP REPLY (RFC 5546): what Outlook, Gmail and Apple Mail send
/// back when a guest clicks Accept, Tentative or Decline on a meeting request: ACCEPTED,
/// TENTATIVE (our Maybe) and DECLINED count, anything else (NEEDS-ACTION, DELEGATED) is no
/// answer. Anything that isn't a REPLY (our own invites, a "propose new time" COUNTER) gives nothing. Who sent the
/// mail, and the attendee's address in it, are never read: the UID alone says who answered.
/// </summary>
public static class ItipReplyParser
{
    /// <summary>A calendar bigger than this isn't a reply to one of our invites.</summary>
    public const int MaxLength = 256 * 1024;

    /// <summary>A reply answers one event, or a few dates of one; more is junk.</summary>
    public const int MaxEvents = 50;

    /// <summary>Every guest's invite UID ends so.</summary>
    private const string UidSuffix = "@dixels";

    /// <param name="calendar">The text/calendar part (or .ics attachment).</param>
    /// <param name="mimeMethod">The part's own <c>method=</c>, for a calendar that leaves METHOD out.</param>
    public static IReadOnlyList<ItipAnswer> Parse(string? calendar, string? mimeMethod = null)
    {
        if (string.IsNullOrWhiteSpace(calendar) || calendar.Length > MaxLength)
        {
            return Array.Empty<ItipAnswer>();
        }

        Calendar? parsed;
        try
        {
            parsed = Calendar.Load(calendar);
        }
        catch (Exception)
        {
            // Not a calendar we can read: someone's junk, not an answer.
            return Array.Empty<ItipAnswer>();
        }

        var method = string.IsNullOrWhiteSpace(parsed?.Method) ? mimeMethod : parsed.Method;
        if (parsed is null || !string.Equals(method?.Trim(), "REPLY", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<ItipAnswer>();
        }

        var answers = new List<ItipAnswer>();
        foreach (var evt in parsed.Events.Take(MaxEvents))
        {
            // Our invites have exactly one attendee, the guest; their reply sends back just them.
            var attendee = evt.Attendees.FirstOrDefault();
            // Only a guest's UID ("…@dixels", see InviteeRow): Ical.Net makes one up for an event
            // that has none.
            if (evt.Uid is null || !evt.Uid.Trim().EndsWith(UidSuffix, StringComparison.Ordinal)
                || attendee is null || evt.DtStamp is null)
            {
                continue;
            }

            InviteeResponseStatus status;
            switch (attendee.ParticipationStatus?.Trim().ToUpperInvariant())
            {
                case "ACCEPTED":
                    status = InviteeResponseStatus.Accepted;
                    break;
                case "DECLINED":
                    status = InviteeResponseStatus.Declined;
                    break;
                case "TENTATIVE":
                    status = InviteeResponseStatus.Maybe;
                    break;
                default:
                    continue;
            }

            answers.Add(new ItipAnswer(
                evt.Uid.Trim(),
                evt.RecurrenceId?.Value,
                evt.RecurrenceId?.IsUtc ?? false,
                status,
                evt.DtStamp.IsUtc ? evt.DtStamp.Value : DateTime.SpecifyKind(evt.DtStamp.Value, DateTimeKind.Utc)));
        }

        return answers;
    }
}
