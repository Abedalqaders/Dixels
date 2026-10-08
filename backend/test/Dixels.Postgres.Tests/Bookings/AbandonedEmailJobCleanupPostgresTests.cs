using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Emails;
using Npgsql;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DistributedLocking;
using Xunit;
using Xunit.Abstractions;

namespace Dixels.Postgres.Bookings;

/// <summary>
/// The abandoned-email-jobs part of the hourly cleanup on real Postgres: each batch is one
/// DELETE, and it stays cheap without an index of its own even after a long mail outage.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AbandonedEmailJobCleanupPostgresTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    // A week-long mail outage on a busy site: every email abandoned, kept 90 days.
    private const int OutageJobs = 20_000;

    private static readonly string SendEmail = BackgroundJobNameAttribute.GetName<SendEmailArgs>();

    private readonly ITestOutputHelper _output;

    public AbandonedEmailJobCleanupPostgresTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Straight SQL: <paramref name="count"/> email jobs named <paramref name="name"/>, created
    /// <paramref name="daysAgo"/> days ago (a minute apart). Returns their marker in JobArgs.
    /// </summary>
    private static async Task<string> SeedAsync(int count, double daysAgo, bool abandoned = true, string? name = null)
    {
        var marker = Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var seed = new NpgsqlCommand(
            """
            INSERT INTO "AbpBackgroundJobs" ("Id", "JobName", "JobArgs", "TryCount", "CreationTime", "NextTryTime", "IsAbandoned",
                "Priority", "ExtraProperties", "ConcurrencyStamp")
            SELECT gen_random_uuid(), @name, '{"marker":"' || @marker || '","body":"<p>Hi Guest, guest@outside.io</p>"}', 5,
                   @created - make_interval(mins => n), @created, @abandoned, 15, '{}', ''
            FROM generate_series(1, @count) AS n;
            ANALYZE "AbpBackgroundJobs";
            """, connection);
        seed.Parameters.AddWithValue("name", name ?? SendEmail);
        seed.Parameters.AddWithValue("marker", marker);
        seed.Parameters.AddWithValue("created", DateTime.UtcNow.AddDays(-daysAgo));
        seed.Parameters.AddWithValue("abandoned", abandoned);
        seed.Parameters.AddWithValue("count", count);
        await seed.ExecuteNonQueryAsync();
        return marker;
    }

    private static async Task<long> CountAsync(string marker)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("""SELECT count(*) FROM "AbpBackgroundJobs" WHERE "JobArgs" LIKE '%' || @marker || '%'""", connection);
        count.Parameters.AddWithValue("marker", marker);
        return (long)(await count.ExecuteScalarAsync())!;
    }

    /// <summary>EXPLAIN ANALYZE of the captured DELETE inside a transaction that's rolled back, so nothing goes.</summary>
    private async Task<string> ExplainAsync(NpgsqlCommand captured)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, COSTS OFF) " + captured.CommandText, connection, transaction);
        foreach (NpgsqlParameter parameter in captured.Parameters)
        {
            explain.Parameters.Add(parameter.Clone());
        }

        var lines = new List<string>();
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                lines.Add(reader.GetString(0));
            }
        }

        await transaction.RollbackAsync();

        var plan = string.Join(Environment.NewLine, lines);
        _output.WriteLine(captured.CommandText);
        _output.WriteLine(plan);
        return plan;
    }

    [PostgresFact]
    public async Task A_batch_is_one_cheap_delete_even_after_a_long_outage()
    {
        var outage = await SeedAsync(OutageJobs, daysAgo: 100);
        var retrying = await SeedAsync(50, daysAgo: 100, abandoned: false);
        var other = await SeedAsync(50, daysAgo: 100, name: BackgroundJobNameAttribute.GetName<CancelBookingsInRemovedRoomsArgs>());

        SqlCapture.Instance.Clear();
        var deleted = await WithUnitOfWorkAsync(() => GetRequiredService<IAbandonedJobRepository>().DeleteAbandonedAsync(
            ExternalGuestCleanup.EmailJobNames, DateTime.UtcNow.AddDays(-90), ExternalGuestCleanup.BatchSize));

        deleted.ShouldBe(ExternalGuestCleanup.BatchSize);
        (await CountAsync(outage)).ShouldBe(OutageJobs - ExternalGuestCleanup.BatchSize);

        // One statement for the batch: no rows (and their emails) loaded into the app.
        var delete = SqlCapture.Instance.Commands.ShouldHaveSingleItem();
        delete.CommandText.ShouldContain("DELETE FROM \"AbpBackgroundJobs\"");

        // No index of its own: one pass over the table, which holds only jobs waiting or abandoned.
        var plan = await ExplainAsync(delete);
        var time = double.Parse(plan.Split(Environment.NewLine).Single(l => l.StartsWith("Execution Time:"))
            .Split(' ')[2], System.Globalization.CultureInfo.InvariantCulture);
        time.ShouldBeLessThan(1000);

        // The full run takes the rest; jobs still retried and other jobs stay.
        await GetRequiredService<ExternalGuestCleanup>().RunAsync();
        (await CountAsync(outage)).ShouldBe(0);
        (await CountAsync(retrying)).ShouldBe(50);
        (await CountAsync(other)).ShouldBe(50);
    }

    [PostgresFact]
    public async Task A_server_without_the_lock_leaves_the_jobs()
    {
        var old = await SeedAsync(3, daysAgo: 100);

        await using (var otherServer = await GetRequiredService<IAbpDistributedLock>().TryAcquireAsync(ExternalGuestCleanup.LockName))
        {
            otherServer.ShouldNotBeNull();
            (await GetRequiredService<ExternalGuestCleanup>().RunAsync()).ShouldBe(0);
            (await CountAsync(old)).ShouldBe(3);
        }

        await GetRequiredService<ExternalGuestCleanup>().RunAsync();
        (await CountAsync(old)).ShouldBe(0);
    }
}
