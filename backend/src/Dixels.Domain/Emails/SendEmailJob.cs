using System;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Dixels.Bookings;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Dixels.Emails;

/// <summary>An email already written (in its reader's language), and the calendar file it carries.</summary>
[BackgroundJobName("Dixels.Emails.Send")]
public class SendEmailArgs
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Who a reply goes to: the booker.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>The sender's name, "{booker} (via Dixels)"; the address is always Dixels' own.</summary>
    public string? FromName { get; set; }

    /// <summary>The .ics text, if it carries one, and its method (PUBLISH, REQUEST, CANCEL).</summary>
    public string? Ics { get; set; }
    public string? IcsMethod { get; set; }

    /// <summary>
    /// A guest's email: their calendar UID. The email is dropped if, by the time it's sent,
    /// they're no longer a guest or there's nothing left to come to — the booker can take
    /// someone off the list, or cancel, seconds after booking.
    /// </summary>
    public string? GuestIcsUid { get; set; }
}

/// <summary>
/// Sends an email written when the booking saved: queued in the booking's own transaction
/// (no booking, no email), sent here, and retried by the job queue if the mail server is
/// down. ABP's own queue can't carry an attachment, hence this one. The calendar file goes
/// both as the text/calendar part (what Outlook and Gmail read for "Add to calendar") and as
/// an invite.ics attachment (for everything else).
/// </summary>
public class SendEmailJob : AsyncBackgroundJob<SendEmailArgs>, ITransientDependency
{
    private readonly IEmailSender _emailSender;
    private readonly ISettingProvider _settingProvider;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IClock _clock;

    public SendEmailJob(
        IEmailSender emailSender,
        ISettingProvider settingProvider,
        IBookingRepository bookingRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IClock clock)
    {
        _emailSender = emailSender;
        _settingProvider = settingProvider;
        _bookingRepository = bookingRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _clock = clock;
    }

    public override async Task ExecuteAsync(SendEmailArgs args)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        if (args.GuestIcsUid is { } uid
            && !await _bookingRepository.IsUpcomingGuestAsync(uid, new DateTimeOffset(_clock.Now.ToUniversalTime(), TimeSpan.Zero)))
        {
            return;
        }

        await _emailSender.SendAsync(await ToMailAsync(args));
        await uow.CompleteAsync();
    }

    private async Task<MailMessage> ToMailAsync(SendEmailArgs args)
    {
        var mail = new MailMessage
        {
            Subject = args.Subject,
            Body = args.Body,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
        };
        mail.To.Add(args.To);

        var fromAddress = await _settingProvider.GetOrNullAsync(EmailSettingNames.DefaultFromAddress);
        if (!string.IsNullOrWhiteSpace(fromAddress) && !string.IsNullOrWhiteSpace(args.FromName))
        {
            mail.From = new MailAddress(fromAddress, args.FromName);
        }

        if (!string.IsNullOrWhiteSpace(args.ReplyTo))
        {
            mail.ReplyToList.Add(args.ReplyTo);
        }

        if (args.Ics is { } ics)
        {
            // The HTML body and this become one multipart/alternative: what calendar apps look for.
            var method = args.IcsMethod ?? IcsMethods.Publish;
            var calendarType = new ContentType("text/calendar") { CharSet = "utf-8" };
            calendarType.Parameters.Add("method", method);
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(ics, calendarType));

            var attachmentType = new ContentType("application/ics") { Name = "invite.ics" };
            mail.Attachments.Add(new Attachment(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(ics)), attachmentType));
        }

        return mail;
    }
}
