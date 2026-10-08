using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dixels.Settings;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Dixels.Emails.Rsvp;

/// <summary>
/// The rsvp@ mailbox over IMAP (MailKit), any provider. Its inbox's unread mails are the
/// unhandled ones; handling one marks it read (\Seen). Waits with IMAP IDLE, so an answer is
/// picked up within seconds, and polls every minute when the server has no IDLE (or doesn't
/// use it). Connects on first use with the <c>Dixels.Emails.RsvpMailbox.Imap.*</c> settings,
/// and again after a drop.
/// </summary>
[ExposeServices(typeof(IRsvpMailbox))]
public sealed class ImapRsvpMailbox : IRsvpMailbox, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly ImapClient _client = new();

    public ImapRsvpMailbox(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public async Task<IReadOnlyList<RsvpMailMessage>> GetUnhandledAsync(int max, CancellationToken cancellationToken)
    {
        var inbox = await OpenInboxAsync(cancellationToken);
        var unread = (await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken))
            .OrderBy(uid => uid.Id)
            .Take(max)
            .ToList();
        if (unread.Count == 0)
        {
            return Array.Empty<RsvpMailMessage>();
        }

        // Only the structure first: the calendar part is fetched on its own, never a whole mail.
        var summaries = await inbox.FetchAsync(unread,
            MessageSummaryItems.UniqueId | MessageSummaryItems.Size | MessageSummaryItems.BodyStructure, cancellationToken);

        var messages = new List<RsvpMailMessage>();
        foreach (var summary in summaries.OrderBy(s => s.UniqueId.Id))
        {
            var id = summary.UniqueId.Id.ToString();
            var part = CalendarPart(summary);
            if (part is null || summary.Size > ItipReplyParser.MaxLength * 4 || part.Octets > ItipReplyParser.MaxLength * 2)
            {
                messages.Add(new RsvpMailMessage(id, null, null));
                continue;
            }

            var entity = await inbox.GetBodyPartAsync(summary.UniqueId, part, cancellationToken);
            messages.Add(new RsvpMailMessage(id, Text(entity), part.ContentType.Parameters["method"]));
        }

        return messages;
    }

    public async Task MarkHandledAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var inbox = await OpenInboxAsync(cancellationToken);
        var uids = ids.Select(id => new UniqueId(uint.Parse(id))).ToList();
        await inbox.StoreAsync(uids, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Seen) { Silent = true }, cancellationToken);
    }

    public async Task WaitForNewAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        var inbox = await OpenInboxAsync(cancellationToken);
        if (!_client.Capabilities.HasFlag(ImapCapabilities.Idle))
        {
            await Task.Delay(pollInterval, cancellationToken);
            return;
        }

        // IDLE until the inbox grows, but never longer than a poll: some servers (smtp4dev
        // among them) accept IDLE and then never say a mail came. The caller then looks again.
        using var done = new CancellationTokenSource(pollInterval);
        void OnCountChanged(object? sender, EventArgs e) => done.Cancel();
        inbox.CountChanged += OnCountChanged;
        try
        {
            await _client.IdleAsync(done.Token, cancellationToken);
        }
        finally
        {
            inbox.CountChanged -= OnCountChanged;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(true);
            }
        }
        catch (Exception)
        {
            // Closing a connection that's already gone: nothing to do.
        }

        _client.Dispose();
    }

    private async Task<IMailFolder> OpenInboxAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
        {
            var host = await RequiredAsync(DixelsSettings.RsvpMailboxImapHost);
            var port = int.Parse(await _settingProvider.GetOrNullAsync(DixelsSettings.RsvpMailboxImapPort) ?? "993");
            var useSsl = bool.Parse(await _settingProvider.GetOrNullAsync(DixelsSettings.RsvpMailboxImapUseSsl) ?? "true");
            await _client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        }

        if (!_client.IsAuthenticated)
        {
            var userName = await RequiredAsync(DixelsSettings.RsvpMailboxImapUserName);
            var password = await _settingProvider.GetOrNullAsync(DixelsSettings.RsvpMailboxImapPassword);
            await _client.AuthenticateAsync(userName, password ?? string.Empty, cancellationToken);
        }

        if (!_client.Inbox.IsOpen || _client.Inbox.Access != FolderAccess.ReadWrite)
        {
            await _client.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        }

        return _client.Inbox;
    }

    /// <summary>A setting the mailbox can't be reached without; missing, the worker logs this and tries again later.</summary>
    private async Task<string> RequiredAsync(string name)
    {
        var value = await _settingProvider.GetOrNullAsync(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"The rsvp mailbox needs the setting {name}.")
            : value;
    }

    /// <summary>
    /// The reply's calendar: the text/calendar part mail apps send, else an .ics attachment.
    /// </summary>
    private static BodyPartBasic? CalendarPart(IMessageSummary summary)
    {
        var parts = summary.BodyParts.ToList();
        return parts.FirstOrDefault(p => p.ContentType.IsMimeType("text", "calendar"))
               ?? parts.FirstOrDefault(p => p.ContentType.IsMimeType("application", "ics")
                                            || (p.FileName?.EndsWith(".ics", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static string? Text(MimeEntity entity)
    {
        switch (entity)
        {
            case TextPart text:
                return text.Text;
            case MimePart { Content: not null } part:
                using (var stream = new MemoryStream())
                {
                    part.Content.DecodeTo(stream);
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            default:
                return null;
        }
    }
}
