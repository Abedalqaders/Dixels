using System;
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

    public async Task<bool> AnyConfirmedOverlapAsync(Guid spaceId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var bookings = await GetQueryableAsync();

        // The same overlap predicate as TimeRange.Overlaps and the exclusion constraint:
        // end-exclusive, confirmed bookings only (cancelled ones release their slot).
        return await bookings.AnyAsync(
            b => b.SpaceId == spaceId
                 && b.Status == BookingStatus.Confirmed
                 && b.StartsAt < end
                 && b.EndsAt > start,
            GetCancellationToken(cancellationToken));
    }

    public async Task<Booking?> FindByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var bookings = await GetQueryableAsync();
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
}
