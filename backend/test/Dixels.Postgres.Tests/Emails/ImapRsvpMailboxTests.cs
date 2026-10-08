using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dixels.Emails.Rsvp;
using Dixels.Settings;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit.Net.Smtp;
using MimeKit;
using Shouldly;
using Volo.Abp.Settings;
using Xunit;

namespace Dixels.Emails;

/// <summary>
/// The rsvp@ mailbox over real IMAP (see ImapRsvpMailbox), against smtp4dev in Docker — the
/// same fake mail server dev uses. Runs with the Postgres suite (it needs Docker too).
/// </summary>
public sealed class ImapRsvpMailboxTests : IAsyncLifetime
{
    private IContainer? _smtp4dev;

    private const string Reply = """
        BEGIN:VCALENDAR
        METHOD:REPLY
        VERSION:2.0
        PRODID:-//Test//EN
        BEGIN:VEVENT
        UID:Xp3abcQ@dixels
        DTSTAMP:20261008T091500Z
        ATTENDEE;PARTSTAT=ACCEPTED:mailto:omar@outside.io
        END:VEVENT
        END:VCALENDAR
        """;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable(PostgresFactAttribute.EnvironmentVariable) != "1")
        {
            return;
        }

        _smtp4dev = new ContainerBuilder()
            .WithImage("rnwood/smtp4dev:v3")
            .WithPortBinding(25, true)
            .WithPortBinding(143, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(143).UntilInternalTcpPortIsAvailable(25))
            .Build();
        await _smtp4dev.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_smtp4dev is not null)
        {
            await _smtp4dev.DisposeAsync();
        }
    }

    /// <summary>Just the IMAP settings ImapRsvpMailbox reads.</summary>
    private sealed class Settings : ISettingProvider
    {
        private readonly Dictionary<string, string?> _values;

        public Settings(string host, int port) => _values = new()
        {
            [DixelsSettings.RsvpMailboxImapHost] = host,
            [DixelsSettings.RsvpMailboxImapPort] = port.ToString(),
            [DixelsSettings.RsvpMailboxImapUseSsl] = "false",
            [DixelsSettings.RsvpMailboxImapUserName] = "rsvp@dixels.local",
            [DixelsSettings.RsvpMailboxImapPassword] = "anything",
        };

        public Task<string?> GetOrNullAsync(string name) => Task.FromResult(_values.GetValueOrDefault(name));

        public Task<List<SettingValue>> GetAllAsync(string[] names) =>
            Task.FromResult(names.Select(n => new SettingValue(n, _values.GetValueOrDefault(n))).ToList());

        public Task<List<SettingValue>> GetAllAsync() =>
            Task.FromResult(_values.Select(v => new SettingValue(v.Key, v.Value)).ToList());
    }

    private ImapRsvpMailbox NewMailbox() =>
        new(new Settings(_smtp4dev!.Hostname, _smtp4dev.GetMappedPublicPort(143)));

    private async Task SendAsync(string subject, Action<BodyBuilder, Multipart?> build, bool calendarAsAlternative = false)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("omar@outside.io"));
        message.To.Add(MailboxAddress.Parse("rsvp@dixels.local"));
        message.Subject = subject;

        if (calendarAsAlternative)
        {
            // What Outlook sends: the text and the calendar side by side.
            var calendar = new TextPart("calendar") { Text = Reply };
            calendar.ContentType.Parameters.Add("method", "REPLY");
            message.Body = new MultipartAlternative { new TextPart("plain") { Text = "Omar has accepted." }, calendar };
        }
        else
        {
            var body = new BodyBuilder { TextBody = "Hello" };
            build(body, null);
            message.Body = body.ToMessageBody();
        }

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_smtp4dev!.Hostname, _smtp4dev.GetMappedPublicPort(25), MailKit.Security.SecureSocketOptions.None);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }

    [PostgresFact]
    public async Task Replies_are_read_by_their_calendar_part_and_handled_ones_are_not_read_again()
    {
        await SendAsync("Accepted: Q4 planning", (_, _) => { }, calendarAsAlternative: true);
        await SendAsync("Just a note", (_, _) => { });
        await SendAsync("Accepted (attachment)", (body, _) => body.Attachments.Add("invite.ics", Encoding.UTF8.GetBytes(Reply), ContentType.Parse("application/ics")));

        await using var mailbox = NewMailbox();
        var waiting = await mailbox.GetUnhandledAsync(10, CancellationToken.None);

        waiting.Count.ShouldBe(3);
        waiting[0].Calendar.ShouldNotBeNull().ShouldContain("METHOD:REPLY");
        waiting[0].CalendarMethod.ShouldBe("REPLY");
        ItipReplyParser.Parse(waiting[0].Calendar, waiting[0].CalendarMethod).ShouldHaveSingleItem().Uid.ShouldBe("Xp3abcQ@dixels");
        waiting[1].Calendar.ShouldBeNull();
        ItipReplyParser.Parse(waiting[2].Calendar).ShouldHaveSingleItem();

        await mailbox.MarkHandledAsync(waiting.Select(m => m.Id).ToList(), CancellationToken.None);
        (await mailbox.GetUnhandledAsync(10, CancellationToken.None)).ShouldBeEmpty();

        // A fresh connection (a restart) doesn't see them either.
        await using var again = NewMailbox();
        (await again.GetUnhandledAsync(10, CancellationToken.None)).ShouldBeEmpty();
    }

    [PostgresFact]
    public async Task Waiting_returns_once_a_new_mail_arrives()
    {
        await using var mailbox = NewMailbox();
        (await mailbox.GetUnhandledAsync(10, CancellationToken.None)).ShouldBeEmpty();

        var timer = Stopwatch.StartNew();
        var wait = mailbox.WaitForNewAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await SendAsync("Accepted: Q4 planning", (_, _) => { }, calendarAsAlternative: true);
        await wait.WaitAsync(TimeSpan.FromSeconds(30));

        // Within the poll interval at the latest: smtp4dev takes IDLE but never says a mail came.
        timer.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
        (await mailbox.GetUnhandledAsync(10, CancellationToken.None)).ShouldHaveSingleItem().Calendar.ShouldNotBeNull();
    }
}
