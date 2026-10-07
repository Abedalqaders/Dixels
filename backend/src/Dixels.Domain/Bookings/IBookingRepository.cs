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

    /// <summary>
    /// The same kind of lock on the person, for buildings that allow one booking at a time:
    /// two of their own requests (two tabs) are checked one after the other, so both can't
    /// read "no clash" at once.
    /// </summary>
    Task LockUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> AnyConfirmedOverlapAsync(Guid spaceId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    /// <summary>Every confirmed booking on any of these spaces that overlaps <c>[start, end)</c> — one query for a whole building's day.</summary>
    Task<List<Booking>> GetConfirmedOverlappingAsync(
        IReadOnlyCollection<Guid> spaceIds,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    /// <summary>The user's confirmed bookings overlapping <c>[start, end)</c>, earliest first.</summary>
    Task<List<Booking>> GetConfirmedForUserAsync(
        Guid userId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What the person's calendar shows for <c>[start, end)</c>: their confirmed bookings, and
    /// the ones an admin cancelled that haven't ended yet (so they see why a booking went).
    /// With <paramref name="buildingId"/>, only those in that building (deleted rooms
    /// included) — an employee books in one building, so their calendar is that building's.
    /// </summary>
    Task<List<Booking>> GetCalendarForUserAsync(
        Guid userId,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset now,
        Guid? buildingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirmed bookings starting in <c>(after, until]</c> whose reminder hasn't been sent,
    /// earliest first, at most <paramref name="maxCount"/> and none of <paramref name="skipIds"/> —
    /// what the reminder job sends next. Bookings in a removed room, floor or building are left out.
    /// </summary>
    Task<List<Booking>> GetDueForReminderAsync(DateTimeOffset after, DateTimeOffset until, int maxCount, IReadOnlyCollection<Guid> skipIds, CancellationToken cancellationToken = default);

    Task<Booking?> FindByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts and saves immediately. If the database's no-overlap constraint rejects the
    /// row (a race the lock didn't cover), this throws the same <c>Overlap</c> business
    /// error the validator would have produced, and the whole transaction rolls back.
    /// </summary>
    Task<Booking> InsertConfirmedAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same for many at once (a series' dates): one save, which the database receives in
    /// a few batched round trips instead of one per row. If any row is rejected, none is
    /// saved and the same <c>Overlap</c> error is thrown.
    /// </summary>
    Task InsertManyConfirmedAsync(IReadOnlyCollection<Booking> bookings, CancellationToken cancellationToken = default);
}
