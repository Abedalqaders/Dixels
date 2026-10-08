using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dixels.Emails.Rsvp;

/// <summary>
/// One mail in the rsvp@ mailbox, reduced to what an answer needs: its calendar part. Null
/// <see cref="Calendar"/>: it has none (or it's too big to be an answer), so it's just marked handled.
/// </summary>
public sealed record RsvpMailMessage(string Id, string? Calendar, string? CalendarMethod);

/// <summary>
/// The mailbox guests' mail apps send their Accept / Decline to. Kept this small so IMAP
/// (<see cref="ImapRsvpMailbox"/>) can later be swapped for a Microsoft 365 or Google mailbox
/// connection without touching the rest. One instance is one connection, used by one reader.
/// </summary>
public interface IRsvpMailbox : IAsyncDisposable
{
    /// <summary>The oldest mails not handled yet, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<RsvpMailMessage>> GetUnhandledAsync(int max, CancellationToken cancellationToken);

    /// <summary>Marks mails done, so they're never read again. Only once their answers are saved.</summary>
    Task MarkHandledAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Returns when new mail may have arrived: as soon as the server says so, if it can, else
    /// after <paramref name="pollInterval"/>.
    /// </summary>
    Task WaitForNewAsync(TimeSpan pollInterval, CancellationToken cancellationToken);
}
