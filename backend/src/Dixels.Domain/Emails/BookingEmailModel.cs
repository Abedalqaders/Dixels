using System.Collections.Generic;

namespace Dixels.Emails;

/// <summary>
/// What a booking email shows, already worded in the recipient's language. Templates read it
/// as <c>model.space_name</c> etc. (Scriban's snake_case names for these properties); the
/// layout reads the same model, for the band, greeting, heading and When / Where blocks.
/// </summary>
public class BookingEmailModel
{
    /// <summary>The band's status: one of <see cref="EmailStatus"/>.</summary>
    public string Status { get; set; } = EmailStatus.Confirmed;

    public string RecipientName { get; set; } = string.Empty;

    /// <summary>The big line under the greeting: "You're booked: Q4 planning".</summary>
    public string Heading { get; set; } = string.Empty;

    /// <summary>Strikes the When of every block through (a cancellation).</summary>
    public bool Cancelled { get; set; }

    /// <summary>The When / Where blocks, soonest first: one for most emails, one per booking for an admin cancel.</summary>
    public List<BookingEmailRow> Rows { get; set; } = new();

    // The first (or only) booking's parts, for subjects and detail rows.
    public string SpaceName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>The building's address in the shown language; its detail row only when set.</summary>
    public string? Address { get; set; }

    /// <summary>"Fri 2 Oct 2026", or for several "Fri 2 Oct 2026 – Fri 30 Oct 2026". Building-local.</summary>
    public string Date { get; set; } = string.Empty;

    public int Attendees { get; set; }
    public string? Title { get; set; }

    /// <summary>How many bookings the email is about: 1, or a series' dates.</summary>
    public int Count { get; set; }

    /// <summary>A cancellation: why (the employee's own words, or the admin action's).</summary>
    public string? Reason { get; set; }

    /// <summary>A series: "From Mon 12 Oct 2026".</summary>
    public string? RepeatsFrom { get; set; }

    /// <summary>A series: the dates its rule lands on that weren't booked ("Mon 2 Nov 2026, …").</summary>
    public string? NotOn { get; set; }

    /// <summary>An admin cancel: how many more were cancelled than <see cref="Rows"/> shows.</summary>
    public int More { get; set; }

    /// <summary>"View booking": My calendar on the (first) booking's day.</summary>
    public string ViewUrl { get; set; } = string.Empty;

    /// <summary>"Find another room".</summary>
    public string FindUrl { get; set; } = string.Empty;

    /// <summary>The booker's own email: their guests, by name ("Omar Farouk (guest)"). Null for none.</summary>
    public string? Guests { get; set; }

    /// <summary>A guest's email: who invited them.</summary>
    public string? InvitedBy { get; set; }

    /// <summary>A guest's email: the other guests, by name only. Null for none.</summary>
    public string? AlsoInvited { get; set; }

    /// <summary>A guest's email: whether they have a Dixels account to open it in (colleagues do, outsiders don't).</summary>
    public bool CanOpen { get; set; }

    /// <summary>A guest's email: about a series (repeats, from, not on).</summary>
    public bool IsSeries { get; set; }

    /// <summary>The footer line, when not the usual "about your bookings".</summary>
    public string? Footer { get; set; }

    /// <summary>A guest's cancel email: an admin's action, not the booker's (the reason goes in the red box).</summary>
    public bool CancelledByAdmin { get; set; }

    /// <summary>A guest's email: the booker took them off the list (the meeting goes on).</summary>
    public bool RemovedByOwner { get; set; }

    /// <summary>The booker's cancel email: how many guests were told it's off (0: no line).</summary>
    public int GuestsTold { get; set; }

    /// <summary>A guest's reminder, when they haven't answered: "Sara Ali hasn't heard from you yet…", above the Yes / No buttons.</summary>
    public string? AnswerNudge { get; set; }

    /// <summary>A guest's invite: their own Accept / Decline links (the public answer page).</summary>
    public string? AcceptUrl { get; set; }
    public string? DeclineUrl { get; set; }

    /// <summary>
    /// The invite is a meeting request the guest's mail app answers itself (E6): the email then
    /// points at its Accept / Decline bar, and the links above shrink to a backup line to
    /// <see cref="AnswerUrl"/> (the answer page, no answer chosen yet).
    /// </summary>
    public bool AnswerInMailApp { get; set; }
    public string? AnswerUrl { get; set; }

    /// <summary>The booker's "can't make it" email: the guest who declined.</summary>
    public string? AnsweredBy { get; set; }

    /// <summary>The calendar file the email carries, if any (not shown; attached by <see cref="SendEmailJob"/>).</summary>
    public IcsEvent? Calendar { get; set; }
}

/// <summary>One When / Where block, worded like the model (<c>row.space_name</c>…).</summary>
public class BookingEmailRow
{
    public string SpaceName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>"Thu 8 Oct 2026", or a series' "Every Monday until Mon 30 Nov 2026".</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>"10:00–11:00", building-local.</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>The building's zone at that time: "Amman time", or in Arabic "GMT+3".</summary>
    public string Zone { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Reason { get; set; }
}

/// <summary>The band's status words (their texts are <c>Email:Status:{status}</c>).</summary>
public static class EmailStatus
{
    public const string Confirmed = "Confirmed";
    public const string Reminder = "Reminder";
    public const string Invitation = "Invitation";
    public const string Cancelled = "Cancelled";
    public const string Updated = "Updated";
    public const string Declined = "Declined";
}
