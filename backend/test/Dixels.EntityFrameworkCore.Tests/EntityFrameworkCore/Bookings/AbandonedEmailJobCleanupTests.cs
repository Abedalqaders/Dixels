using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Emails;
using Dixels.Settings;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.SettingManagement;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// Email jobs ABP abandoned hold a rendered email (names, addresses), so the hourly cleanup
/// deletes them after the same retention days as outside guests. Jobs still being retried and
/// other kinds of job stay.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class AbandonedEmailJobCleanupTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private static readonly string SendEmail = BackgroundJobNameAttribute.GetName<SendEmailArgs>();

    private readonly IBackgroundJobRepository _jobs;
    private readonly ExternalGuestCleanup _cleanup;

    public AbandonedEmailJobCleanupTests()
    {
        _jobs = GetRequiredService<IBackgroundJobRepository>();
        _cleanup = GetRequiredService<ExternalGuestCleanup>();
    }

    /// <summary>A job created <paramref name="daysAgo"/> days ago, as ABP stores it.</summary>
    private static BackgroundJobRecord NewJob(string name, double daysAgo, bool abandoned = true)
    {
        var created = DateTime.UtcNow.AddDays(-daysAgo);
        return new BackgroundJobRecord(Guid.NewGuid())
        {
            JobName = name,
            JobArgs = """{"to":"guest@outside.io","body":"<p>Hi Guest</p>"}""",
            CreationTime = created,
            NextTryTime = created.AddMinutes(1),
            TryCount = 5,
            IsAbandoned = abandoned,
        };
    }

    private Task<Guid> InsertJobAsync(string name, double daysAgo, bool abandoned = true) => WithUnitOfWorkAsync(async () =>
        (await _jobs.InsertAsync(NewJob(name, daysAgo, abandoned))).Id);

    private Task<bool> ExistsAsync(Guid id) => WithUnitOfWorkAsync(async () => await _jobs.FindAsync(id) is not null);

    private Task SetRetentionAsync(string? days) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ISettingManager>().SetGlobalAsync(DixelsSettings.ExternalGuestRetentionDays, days));

    [Fact]
    public void Every_email_job_is_on_the_list()
    {
        ExternalGuestCleanup.EmailJobNames.ShouldBe(new[]
        {
            "Dixels.Emails.Send",
            "Dixels.Emails.AdminCancelled",
            "Dixels.Emails.AdminCancelledGuest",
            "Volo.Abp.Emailing.BackgroundEmailSendingJobArgs",
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task Abandoned_email_jobs_go_after_90_days()
    {
        var old = new List<Guid>();
        foreach (var name in ExternalGuestCleanup.EmailJobNames)
        {
            old.Add(await InsertJobAsync(name, daysAgo: 91));
        }
        var recent = await InsertJobAsync(SendEmail, daysAgo: 89);

        (await _cleanup.RunAsync()).ShouldBeGreaterThanOrEqualTo(old.Count);

        foreach (var id in old)
        {
            (await ExistsAsync(id)).ShouldBeFalse();
        }
        (await ExistsAsync(recent)).ShouldBeTrue();
    }

    [Fact]
    public async Task Jobs_still_being_retried_and_other_jobs_stay()
    {
        var retrying = await InsertJobAsync(SendEmail, daysAgo: 200, abandoned: false);
        var otherJob = await InsertJobAsync(BackgroundJobNameAttribute.GetName<CancelBookingsInRemovedRoomsArgs>(), daysAgo: 200);

        await _cleanup.RunAsync();

        (await ExistsAsync(retrying)).ShouldBeTrue();
        (await ExistsAsync(otherJob)).ShouldBeTrue();
    }

    [Fact]
    public async Task The_setting_moves_the_cutoff_and_zero_turns_it_off()
    {
        var fortyDaysAgo = await InsertJobAsync(SendEmail, daysAgo: 40);
        var yearAgo = await InsertJobAsync(SendEmail, daysAgo: 365);

        try
        {
            await SetRetentionAsync("0");
            (await _cleanup.RunAsync()).ShouldBe(0);
            (await ExistsAsync(yearAgo)).ShouldBeTrue();

            await SetRetentionAsync("30");
            await _cleanup.RunAsync();
            (await ExistsAsync(fortyDaysAgo)).ShouldBeFalse();
            (await ExistsAsync(yearAgo)).ShouldBeFalse();
        }
        finally
        {
            await SetRetentionAsync(null);
        }
    }

    [Fact]
    public async Task Many_jobs_go_in_batches()
    {
        // Two and a half batches.
        var jobs = Enumerable.Range(0, ExternalGuestCleanup.BatchSize * 5 / 2).Select(i => NewJob(SendEmail, daysAgo: 100 + i % 50)).ToList();
        await WithUnitOfWorkAsync(() => _jobs.InsertManyAsync(jobs));
        var ids = jobs.Select(j => j.Id).ToList();

        (await _cleanup.RunAsync()).ShouldBeGreaterThanOrEqualTo(ids.Count);

        (await WithUnitOfWorkAsync(async () =>
            await (await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync())
                .Set<BackgroundJobRecord>().CountAsync(j => ids.Contains(j.Id)))).ShouldBe(0);
    }
}
