using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Dixels.Emails;

/// <summary>Whose bookings an admin cancelled: the ones the email lists, and how many in all.</summary>
[BackgroundJobName("Dixels.Emails.AdminCancelled")]
public class AdminCancelledEmailArgs
{
    public Guid UserId { get; set; }

    /// <summary>The soonest few (<see cref="AdminCancelEmailQueue.ShownBookings"/>), to list.</summary>
    public List<Guid> BookingIds { get; set; } = new();

    /// <summary>How many were cancelled, listed or not.</summary>
    public int Count { get; set; }

    /// <summary>How many of their guests were told.</summary>
    public int GuestsTold { get; set; }
}

/// <summary>
/// Writes and sends one person's "an admin cancelled your bookings" email. It's written here,
/// not in the admin's save, so a change that cancels hundreds of people's bookings only adds
/// a job each to that save. A send that fails is retried by the job queue.
/// </summary>
public class AdminCancelledEmailJob : AsyncBackgroundJob<AdminCancelledEmailArgs>, ITransientDependency
{
    private readonly BookingEmails _bookingEmails;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public AdminCancelledEmailJob(BookingEmails bookingEmails, IUnitOfWorkManager unitOfWorkManager)
    {
        _bookingEmails = bookingEmails;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public override async Task ExecuteAsync(AdminCancelledEmailArgs args)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        await _bookingEmails.SendAdminCancelledAsync(args.UserId, args.BookingIds, args.Count, args.GuestsTold);
        await uow.CompleteAsync();
    }
}
