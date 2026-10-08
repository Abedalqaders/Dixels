using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Guids;

namespace Dixels.Bookings;

/// <summary>
/// A recurring booking: the rule and the details every occurrence shares. The occurrences
/// themselves are ordinary <see cref="Booking"/> rows (with <see cref="Booking.SeriesId"/>),
/// materialised when the series is created — so each one is checked, locked, listed and
/// cancelled exactly like a single booking, and the database's no-overlap rule covers them
/// all. Dates skipped at creation (or that failed a rule) simply have no row.
/// </summary>
public class BookingSeries : AuditedAggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }
    public string Title { get; private set; } = null!;
    public int Attendees { get; private set; }

    /// <summary>The first date asked for (local), which anchors the rule — "every 2 weeks" counts from its week.</summary>
    public DateOnly FirstDate { get; private set; }

    /// <summary>Local start time of every occurrence, and how long each lasts.</summary>
    public TimeOnly StartTime { get; private set; }
    public int DurationMinutes { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }
    public int Interval { get; private set; }
    public int WeekdaysMask { get; private set; }
    public MonthlyRepeat MonthlyRepeat { get; private set; }
    public DateOnly EndDate { get; private set; }

    /// <summary>A retry of the same create (same key) returns this series instead of making a second.</summary>
    public string IdempotencyKey { get; private set; } = null!;

    /// <summary>Who's invited to every date; each new date gets a copy (see <see cref="Booking.Invitees"/>).</summary>
    public IReadOnlyCollection<BookingSeriesAttendee> Invitees => _invitees;
    private readonly List<BookingSeriesAttendee> _invitees = new();

    private BookingSeries()
    {
        // EF Core
    }

    public BookingSeries(
        Guid id,
        Guid userId,
        Guid spaceId,
        string title,
        int attendees,
        DateOnly firstDate,
        TimeOnly startTime,
        int durationMinutes,
        RecurrenceRule rule,
        string idempotencyKey)
        : base(id)
    {
        UserId = userId;
        SpaceId = spaceId;
        // Empty when the employee gave no title, like Booking.Title.
        Title = Check.Length(Check.NotNull(title, nameof(title)), nameof(title), BookingConsts.MaxTitleLength)!;
        Attendees = attendees;
        FirstDate = firstDate;
        StartTime = startTime;
        DurationMinutes = durationMinutes;
        Frequency = rule.Frequency;
        Interval = rule.Interval;
        WeekdaysMask = rule.WeekdaysMask;
        MonthlyRepeat = rule.MonthlyRepeat;
        EndDate = rule.EndDate;
        IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), BookingConsts.MaxSeriesIdempotencyKeyLength);
    }

    public RecurrenceRule Rule => new(Frequency, Interval, RecurrenceRule.WeekdaysFromMask(WeekdaysMask), MonthlyRepeat, EndDate);

    /// <summary>
    /// Makes the series' guest list exactly <paramref name="invitees"/> (already checked) and
    /// returns who was added and who was removed. Its dates each hold their own copy.
    /// </summary>
    public (List<Invitee> Added, List<Invitee> Removed) SetInvitees(IReadOnlyCollection<Invitee> invitees, IGuidGenerator guidGenerator)
    {
        return _invitees.Replace(invitees, invitee => new BookingSeriesAttendee(guidGenerator.Create(), Id, invitee));
    }

    /// <summary>The owner changing the head count and the guest list for the series (its upcoming dates are changed alongside).</summary>
    public (List<Invitee> Added, List<Invitee> Removed) ChangeGuests(int attendees, IReadOnlyCollection<Invitee> invitees, IGuidGenerator guidGenerator)
    {
        if (attendees < 1 + invitees.Count)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingAttendeesBelowInvitees)
                .WithData("invitees", invitees.Count)
                .WithData("attendees", attendees)
                .WithData("needed", 1 + invitees.Count);
        }

        Attendees = attendees;
        return SetInvitees(invitees, guidGenerator);
    }

    /// <summary>Whether a retried create (same key) asks for the same people as this series was made with.</summary>
    public bool MatchesInvitees(IReadOnlyCollection<Invitee> invitees) =>
        Booking.SameInvitees(_invitees.Select(i => i.ToInvitee()), invitees);
}
