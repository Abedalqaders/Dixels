using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Bookings;

/// <summary>
/// One reserved time window on one <see cref="SpaceManagement.Space"/>. Times are UTC
/// instants with an exclusive end (09:00–10:00 and 10:00–11:00 don't clash).
///
/// Audited but not soft-deletable: a booking is never deleted, it's cancelled
/// (<see cref="Status"/> + the cancel fields), so its history stays queryable and the
/// database's no-overlap constraint only has to look at <see cref="BookingStatus.Confirmed"/>
/// rows. CreatorId/CreationTime (from the base class) are the BRS "created-by/created-at".
///
/// <see cref="ResolvedConstraintsJson"/> is the rule set this booking was accepted under.
/// Tightening a constraint later never cancels an existing booking (grandfathering), and
/// this snapshot is what tells a legitimately grandfathered booking apart from one created
/// by a bug.
/// </summary>
public class Booking : AuditedAggregateRoot<Guid>
{
    public Guid SpaceId { get; private set; }

    /// <summary>
    /// Who the booking is for. Separate from CreatorId (who made it) so delegated booking
    /// can be added later without a schema change; today they're always the same user.
    /// </summary>
    public Guid UserId { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public int Attendees { get; private set; }
    public string Title { get; private set; } = null!;
    public BookingStatus Status { get; private set; }
    public string ResolvedConstraintsJson { get; private set; } = null!;

    /// <summary>
    /// Client-generated per submission. A retry with the same key returns the booking the
    /// first attempt created instead of creating a second one.
    /// </summary>
    public string IdempotencyKey { get; private set; } = null!;

    /// <summary>Set on every occurrence of a recurring series (recurring bookings come later).</summary>
    public Guid? SeriesId { get; private set; }

    public Guid? CancelledById { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }

    /// <summary>True when an administrator cancelled someone else's booking (force cancel).</summary>
    public bool CancelledByAdmin { get; private set; }

    private Booking()
    {
        // EF Core
    }

    public Booking(
        Guid id,
        Guid spaceId,
        Guid userId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int attendees,
        string? title,
        string resolvedConstraintsJson,
        string idempotencyKey,
        Guid? seriesId = null)
        : base(id)
    {
        if (endsAt <= startsAt)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingInvalidTimeRange);
        }

        if (attendees < 1)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingAttendeesMustBePositive);
        }

        SpaceId = spaceId;
        UserId = userId;

        // Normalised to offset zero: Postgres stores timestamptz as UTC anyway, and Npgsql
        // refuses to write a DateTimeOffset with a non-zero offset.
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt.ToUniversalTime();
        Attendees = attendees;
        Title = NormalizeTitle(title);
        Status = BookingStatus.Confirmed;
        ResolvedConstraintsJson = Check.NotNullOrWhiteSpace(resolvedConstraintsJson, nameof(resolvedConstraintsJson));
        IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), BookingConsts.MaxIdempotencyKeyLength);
        SeriesId = seriesId;
    }

    /// <summary>
    /// Whether a retried submission (same idempotency key) asks for exactly this booking.
    /// Same key but different details is a client bug or a reused key, not a retry, so it
    /// must be rejected rather than silently answered with this booking.
    /// </summary>
    public bool MatchesRequest(Guid spaceId, DateTimeOffset startsAt, DateTimeOffset endsAt, int attendees)
    {
        return SpaceId == spaceId && StartsAt == startsAt && EndsAt == endsAt && Attendees == attendees;
    }

    /// <summary>
    /// Releases the slot. The row stays (history), with who cancelled, when and why; the
    /// database's no-overlap rule only looks at confirmed rows, so the time is free again
    /// the moment this commits.
    /// </summary>
    public void Cancel(Guid cancelledById, DateTimeOffset cancelledAt, string? reason, bool byAdmin)
    {
        if (Status != BookingStatus.Confirmed)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingNotCancellable)
                .WithData("status", Status.ToString());
        }

        Status = BookingStatus.Cancelled;
        CancelledById = cancelledById;
        CancelledAt = cancelledAt.ToUniversalTime();
        CancelReason = Check.Length(reason?.Trim(), nameof(reason), BookingConsts.MaxCancelReasonLength);
        CancelledByAdmin = byAdmin;
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return BookingConsts.DefaultTitle;
        }

        return Check.Length(title.Trim(), nameof(title), BookingConsts.MaxTitleLength)!;
    }
}
