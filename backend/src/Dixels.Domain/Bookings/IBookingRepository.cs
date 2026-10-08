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
    /// the ones an admin cancelled that haven't ended yet (so they see why a booking went) —
    /// both their own and the ones they're a colleague guest of. A booking the owner cancelled,
    /// or that they were removed from, isn't shown.
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

    /// <summary>
    /// The guests of each of these bookings, in one query: a copy for an event, so a listener
    /// still knows who to tell after the rows change or are cleaned up. Bookings without
    /// guests are left out.
    /// </summary>
    Task<Dictionary<Guid, IReadOnlyList<Invitee>>> GetInviteesAsync(IReadOnlyCollection<Guid> bookingIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The upcoming bookings the person is a colleague guest of (confirmed, not started), each
    /// with its series (null for a single booking), soonest first. Their guest rows are found
    /// through (UserId, EndsAt), so past invitations aren't read. With
    /// <paramref name="buildingId"/>, only those in that building (deleted rooms included).
    /// </summary>
    Task<List<(Guid BookingId, Guid? SeriesId)>> GetUpcomingInvitationsAsync(
        Guid userId,
        DateTimeOffset now,
        Guid? buildingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets what this unit of work has read of these bookings and series (with their guests),
    /// so they're read again fresh: after a save lost a race to someone else's.
    /// </summary>
    Task ForgetAsync(IReadOnlyCollection<Guid> bookingIds, IReadOnlyCollection<Guid> seriesIds, CancellationToken cancellationToken = default);

    /// <summary>The person's answer to each of these bookings they're a colleague guest of, in one query.</summary>
    Task<Dictionary<Guid, InviteeResponseStatus>> GetResponsesAsync(IReadOnlyCollection<Guid> bookingIds, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> outside guests' rows (no UserId) of bookings that
    /// ended, or were cancelled, before <paramref name="cutoff"/>, in one statement; returns how
    /// many. Colleagues' rows and the bookings themselves are untouched.
    /// </summary>
    Task<int> DeleteExpiredExternalGuestsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same for series' own lists: only once none of the series' dates is still inside the
    /// window (each ended, or was cancelled, before <paramref name="cutoff"/>).
    /// </summary>
    Task<int> DeleteExpiredSeriesExternalGuestsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Whether the person is (still) a colleague guest of this booking.</summary>
    Task<bool> IsInviteeAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Whether the person is on this series' own guest list.</summary>
    Task<bool> IsSeriesInviteeAsync(Guid seriesId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether someone, by their calendar UID, is still a guest of something still ahead: a
    /// confirmed booking that hasn't started, or a series with such a date left.
    /// </summary>
    Task<bool> IsUpcomingGuestAsync(string icsUid, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<Booking?> FindByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts and saves immediately. If the database's no-overlap constraint rejects the
    /// row (a race the lock didn't cover), this throws the same <c>Overlap</c> business
    /// error the validator would have produced, and the whole transaction rolls back.
    /// </summary>
    Task<Booking> InsertConfirmedAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels these bookings as an admin in one UPDATE, for a cancel too big to load and save
    /// booking by booking. Only what <see cref="Booking.Cancel"/> would accept, and only what
    /// hasn't started: still confirmed and starting after <paramref name="cancelledAt"/>. Sets
    /// what <c>Cancel</c> and an ABP save would (who, when, why, by an admin, last modified, a
    /// new concurrency stamp). Returns the bookings it cancelled, read back untracked.
    /// </summary>
    Task<List<Booking>> CancelUpcomingAsAdminAsync(
        IReadOnlyCollection<Guid> ids,
        Guid adminId,
        DateTimeOffset cancelledAt,
        string reason,
        DateTime modificationTime,
        Guid? modifierId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same for many at once (a series' dates): one save, which the database receives in
    /// a few batched round trips instead of one per row. If any row is rejected, none is
    /// saved and the same <c>Overlap</c> error is thrown.
    /// </summary>
    Task InsertManyConfirmedAsync(IReadOnlyCollection<Booking> bookings, CancellationToken cancellationToken = default);
}
