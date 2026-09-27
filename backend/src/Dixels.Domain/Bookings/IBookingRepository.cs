using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Bookings;

/// <summary>
/// The booking persistence operations that need more than generic CRUD: the concurrency
/// story (lock + database-enforced no-overlap) lives behind this interface, in the EF layer,
/// so the domain can state what it needs without knowing it's Postgres underneath.
/// </summary>
public interface IBookingRepository : IRepository<Booking, Guid>
{
    /// <summary>
    /// Takes a row lock on the space for the rest of the current transaction, so two
    /// bookings for the same space are evaluated one after the other instead of both
    /// reading "free" at the same moment. Different spaces never wait on each other.
    /// </summary>
    Task LockSpaceAsync(Guid spaceId, CancellationToken cancellationToken = default);

    Task<bool> AnyConfirmedOverlapAsync(Guid spaceId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    /// <summary>Every confirmed booking on any of these spaces that overlaps <c>[start, end)</c> — one query for a whole building's day.</summary>
    Task<List<Booking>> GetConfirmedOverlappingAsync(
        IReadOnlyCollection<Guid> spaceIds,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    Task<Booking?> FindByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts and saves immediately. If the database's no-overlap constraint rejects the
    /// row (a race the lock didn't cover), this throws the same <c>Overlap</c> business
    /// error the validator would have produced, and the whole transaction rolls back.
    /// </summary>
    Task<Booking> InsertConfirmedAsync(Booking booking, CancellationToken cancellationToken = default);
}
