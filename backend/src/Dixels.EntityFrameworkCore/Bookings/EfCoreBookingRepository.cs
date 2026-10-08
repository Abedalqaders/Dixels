using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Volo.Abp;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.Bookings;

public class EfCoreBookingRepository : EfCoreRepository<DixelsDbContext, Booking, Guid>, IBookingRepository
{
    private static readonly string LockSpaceSql =
        $"SELECT 1 FROM \"{DixelsConsts.DbTablePrefix}Spaces\" WHERE \"Id\" = {{0}} FOR UPDATE";

    private const string LockUserSql = "SELECT 1 FROM \"AbpUsers\" WHERE \"Id\" = {0} FOR UPDATE";

    public EfCoreBookingRepository(IDbContextProvider<DixelsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task LockSpaceAsync(Guid spaceId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        // SQLite (the unit-test database) has no row locks — it serialises writers on its
        // own — so this is Postgres-only. The real concurrency guarantee is the exclusion
        // constraint; this lock just makes the common race end in the friendly validator
        // message instead of a constraint error.
        if (dbContext.Database.IsNpgsql())
        {
            await dbContext.Database.ExecuteSqlRawAsync(LockSpaceSql, new object[] { spaceId }, GetCancellationToken(cancellationToken));
        }
    }

    public async Task LockUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        // Postgres only, like LockSpaceAsync — SQLite serialises writers by itself.
        if (dbContext.Database.IsNpgsql())
        {
            await dbContext.Database.ExecuteSqlRawAsync(LockUserSql, new object[] { userId }, GetCancellationToken(cancellationToken));
        }
    }

    public async Task<bool> AnyConfirmedOverlapAsync(Guid spaceId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        return await (await ConfirmedOverlappingAsync(new[] { spaceId }, start, end))
            .AnyAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Booking>> GetConfirmedOverlappingAsync(
        IReadOnlyCollection<Guid> spaceIds,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        return await (await ConfirmedOverlappingAsync(spaceIds, start, end))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    /// <summary>
    /// The rooms' confirmed bookings that overlap [start, end) — on Postgres as a range
    /// overlap, so it's answered by the exclusion constraint's GiST index (one probe per room)
    /// instead of walking each room's whole booking history. Two things make the index
    /// usable: the same tstzrange expression it was built on, and the status written into
    /// the SQL — a partial index is only picked when the planner can see the query's WHERE
    /// matches the index's, which a parameter wouldn't show. unnest + LATERAL rather than
    /// "SpaceId" = ANY(...), which GiST can't take as an index condition: this way the
    /// planner can probe room by room (SpaceId and range together) when that's cheaper.
    /// </summary>
    public static readonly string ConfirmedOverlapSql =
        $"SELECT b.* FROM unnest({{0}}::uuid[]) AS s(\"Id\") " +
        $"CROSS JOIN LATERAL (SELECT * FROM \"{DixelsConsts.DbTablePrefix}Bookings\" " +
        $"WHERE \"SpaceId\" = s.\"Id\" AND \"Status\" = '{nameof(BookingStatus.Confirmed)}' " +
        $"AND tstzrange(\"StartsAt\", \"EndsAt\", '[)') && tstzrange({{1}}, {{2}}, '[)')) AS b";

    private async Task<IQueryable<Booking>> ConfirmedOverlappingAsync(IReadOnlyCollection<Guid> spaceIds, DateTimeOffset start, DateTimeOffset end)
    {
        var dbContext = await GetDbContextAsync();

        // SQLite (the unit tests) has no ranges, and Postgres refuses a range that ends before
        // it starts: both get the same predicate as TimeRange.Overlaps — end-exclusive,
        // confirmed bookings only (cancelled ones release their slot).
        if (!dbContext.Database.IsNpgsql() || end <= start)
        {
            return (await GetQueryableAsync())
                .Where(b => spaceIds.Contains(b.SpaceId)
                            && b.Status == BookingStatus.Confirmed
                            && b.StartsAt < end
                            && b.EndsAt > start);
        }

        // Distinct: unnest returns a room once per time it's named, which would repeat its bookings.
        return dbContext.Bookings.FromSqlRaw(ConfirmedOverlapSql, spaceIds.Distinct().ToArray(), start, end);
    }

    public async Task<List<Booking>> GetDueForReminderAsync(
        DateTimeOffset after,
        DateTimeOffset until,
        int maxCount,
        IReadOnlyCollection<Guid> skipIds,
        CancellationToken cancellationToken = default)
    {
        var bookings = await GetQueryableAsync();

        // Rooms whose room, floor and building all still exist. The soft-delete filter hides
        // removed ones: their bookings are being cancelled (a background job), not reminded.
        var dbContext = await GetDbContextAsync();
        var liveRoomIds =
            from s in dbContext.Spaces
            join f in dbContext.Floors on s.FloorId equals f.Id
            join bl in dbContext.Buildings on f.BuildingId equals bl.Id
            select s.Id;

        return await bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.ReminderSentAt == null
                        && b.StartsAt > after
                        && b.StartsAt <= until
                        && !skipIds.Contains(b.Id)
                        && liveRoomIds.Contains(b.SpaceId))
            .OrderBy(b => b.StartsAt)
            .Take(maxCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Booking>> GetConfirmedForUserAsync(
        Guid userId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var bookings = await GetQueryableAsync();
        return await bookings
            .Where(b => b.UserId == userId
                        && b.Status == BookingStatus.Confirmed
                        && b.StartsAt < end
                        && b.EndsAt > start)
            .OrderBy(b => b.StartsAt)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Booking>> GetCalendarForUserAsync(
        Guid userId,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset now,
        Guid? buildingId = null,
        CancellationToken cancellationToken = default)
    {
        var bookings = await GetQueryableAsync();
        var dbContext = await GetDbContextAsync();
        if (buildingId is not null)
        {
            // IgnoreQueryFilters: a removed building's (soft-deleted) rooms still hold the
            // cancelled bookings its employees should see.
            var roomIds =
                from s in dbContext.Spaces.IgnoreQueryFilters()
                join f in dbContext.Floors.IgnoreQueryFilters() on s.FloorId equals f.Id
                where f.BuildingId == buildingId
                select s.Id;
            bookings = bookings.Where(b => roomIds.Contains(b.SpaceId));
        }

        var shown = bookings.Where(b => b.StartsAt < end
                                        && b.EndsAt > start
                                        && (b.Status == BookingStatus.Confirmed
                                            || (b.Status == BookingStatus.Cancelled && b.CancelledByAdmin && b.EndsAt > now)));

        // Two halves, each on its own index, joined with UNION ALL: mine through
        // (UserId, StartsAt…), and the ones I'm invited to through the guest rows'
        // (UserId, EndsAt) — never every invitation I ever had. A booking can't be in both:
        // nobody can invite themselves.
        var invitedTo = dbContext.Set<BookingAttendee>()
            .Where(a => a.UserId == userId && a.EndsAt > start)
            .Select(a => a.BookingId);
        return await shown.Where(b => b.UserId == userId)
            .Concat(shown.Where(b => invitedTo.Contains(b.Id)))
            .OrderBy(b => b.StartsAt)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<bool> IsInviteeAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Set<BookingAttendee>()
            .AnyAsync(a => a.BookingId == bookingId && a.UserId == userId, GetCancellationToken(cancellationToken));
    }

    public async Task<Dictionary<Guid, IReadOnlyList<Invitee>>> GetInviteesAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken = default)
    {
        if (bookingIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Invitee>>();
        }

        var dbContext = await GetDbContextAsync();
        var rows = await dbContext.Set<BookingAttendee>()
            .AsNoTracking()
            .Where(a => bookingIds.Contains(a.BookingId))
            .ToListAsync(GetCancellationToken(cancellationToken));

        return rows
            .GroupBy(a => a.BookingId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Invitee>)g.Select(a => a.ToInvitee()).ToList());
    }

    public async Task<bool> IsGuestAsync(string icsUid, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Set<BookingAttendee>().AnyAsync(a => a.IcsUid == icsUid, GetCancellationToken(cancellationToken))
               || await dbContext.Set<BookingSeriesAttendee>().AnyAsync(a => a.IcsUid == icsUid, GetCancellationToken(cancellationToken));
    }

    public async Task<Booking?> FindByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        // With its guests: a retry must ask for the same ones.
        var bookings = await WithDetailsAsync();
        return await bookings.FirstOrDefaultAsync(
            b => b.UserId == userId && b.IdempotencyKey == idempotencyKey,
            GetCancellationToken(cancellationToken));
    }

    public async Task<Booking> InsertConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        try
        {
            return await InsertAsync(booking, autoSave: true, GetCancellationToken(cancellationToken));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // Lost a race the lock didn't cover. The failed statement has aborted the
            // transaction, so nothing from this request is committed.
            throw new BusinessException(DixelsDomainErrorCodes.BookingOverlap, innerException: ex);
        }
    }

    public async Task<List<Booking>> CancelUpcomingAsAdminAsync(
        IReadOnlyCollection<Guid> ids,
        Guid adminId,
        DateTimeOffset cancelledAt,
        string reason,
        DateTime modificationTime,
        Guid? modifierId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        // One stamp for the round: it marks exactly the rows this UPDATE changed, to read back.
        var stamp = Guid.NewGuid().ToString("N");

        // In the unit of work's own transaction, like the rest of the save. The tracker is
        // bypassed (that's the point): a copy of one of these already loaded in this unit of work
        // keeps what it read, and being unchanged it's never saved over what this wrote.
        await dbContext.Bookings
            .Where(b => ids.Contains(b.Id) && b.Status == BookingStatus.Confirmed && b.StartsAt > cancelledAt)
            .ExecuteUpdateAsync(u => u
                .SetProperty(b => b.Status, BookingStatus.Cancelled)
                .SetProperty(b => b.CancelledById, adminId)
                .SetProperty(b => b.CancelledAt, cancelledAt)
                .SetProperty(b => b.CancelReason, reason)
                .SetProperty(b => b.CancelledByAdmin, true)
                .SetProperty(b => b.LastModificationTime, modificationTime)
                .SetProperty(b => b.LastModifierId, modifierId)
                .SetProperty(b => b.ConcurrencyStamp, stamp), GetCancellationToken(cancellationToken));

        return await dbContext.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.Id) && b.ConcurrencyStamp == stamp)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task InsertManyConfirmedAsync(IReadOnlyCollection<Booking> bookings, CancellationToken cancellationToken = default)
    {
        try
        {
            await InsertManyAsync(bookings, autoSave: true, GetCancellationToken(cancellationToken));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // As InsertConfirmedAsync: one row lost a race, so the whole save (and request) fails.
            throw new BusinessException(DixelsDomainErrorCodes.BookingOverlap, innerException: ex);
        }
    }
}
