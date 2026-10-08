using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Dixels.Emails;

/// <summary>One guest, one meeting an admin's action cancelled.</summary>
[BackgroundJobName("Dixels.Emails.AdminCancelledGuest")]
public class AdminCancelledGuestEmailArgs
{
    public GuestCancelNotice Notice { get; set; } = new();
}

/// <summary>
/// Writes and sends a guest's "cancelled by an administrator" email with its CANCEL file.
/// Written here, not in the admin's save, as <see cref="AdminCancelledEmailJob"/> is: a
/// change that cancels hundreds of meetings only adds a job each to that save.
/// </summary>
public class AdminCancelledGuestEmailJob : AsyncBackgroundJob<AdminCancelledGuestEmailArgs>, ITransientDependency
{
    private readonly BookingEmails _bookingEmails;
    private readonly CalendarEmailSender _sender;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public AdminCancelledGuestEmailJob(BookingEmails bookingEmails, CalendarEmailSender sender, IUnitOfWorkManager unitOfWorkManager)
    {
        _bookingEmails = bookingEmails;
        _sender = sender;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public override async Task ExecuteAsync(AdminCancelledGuestEmailArgs args)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        if (await _bookingEmails.WriteGuestCancelAsync(args.Notice, byAdmin: true) is { } email)
        {
            await _sender.SendAsync(email);
        }

        await uow.CompleteAsync();
    }
}
