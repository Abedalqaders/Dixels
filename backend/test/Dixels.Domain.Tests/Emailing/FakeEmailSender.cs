using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Volo.Abp.Emailing;

namespace Dixels.Emailing;

/// <summary>
/// Stands in for the real email sender in every test: nothing leaves the process, and a test
/// can check what would have been sent. Queued and directly sent emails both land in
/// <see cref="Sent"/> — tests care what was sent, not how.
/// </summary>
public class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent.ToList();

    public void Clear() => _sent.Clear();

    public Task SendAsync(string to, string? subject, string? body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
        => Record(to, subject, body);

    public Task SendAsync(string from, string to, string? subject, string? body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
        => Record(to, subject, body);

    public Task SendAsync(MailMessage mail, bool normalize = true)
        => Record(mail.To.ToString(), mail.Subject, mail.Body);

    public Task QueueAsync(string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
        => Record(to, subject, body);

    public Task QueueAsync(string from, string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
        => Record(to, subject, body);

    private Task Record(string to, string? subject, string? body)
    {
        _sent.Enqueue(new SentEmail(to, subject ?? "", body ?? ""));
        return Task.CompletedTask;
    }
}

public record SentEmail(string To, string Subject, string Body);
