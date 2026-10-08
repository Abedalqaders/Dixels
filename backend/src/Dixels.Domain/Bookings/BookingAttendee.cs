using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Dixels.Bookings;

/// <summary>
/// The stored shape of one invitee, shared by a booking's list and a series' list: a colleague
/// is only their user id (their name and email are read from the user, so they stay current);
/// an external guest is the email and name as typed. Exactly one of UserId / Email is set.
/// </summary>
public abstract class InviteeRow : Entity<Guid>
{
    public Guid? UserId { get; private set; }
    public string? Email { get; private set; }
    public string? Name { get; private set; }

    /// <summary>Their answer: Pending until they accept or decline (see <see cref="Respond"/>).</summary>
    public InviteeResponseStatus ResponseStatus { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    protected InviteeRow()
    {
        // EF Core
    }

    protected InviteeRow(Guid id, Invitee invitee)
        : base(id)
    {
        if (invitee.UserId is { } userId)
        {
            UserId = userId;
        }
        else
        {
            Email = Check.NotNullOrWhiteSpace(invitee.Email?.Trim(), nameof(invitee.Email), BookingConsts.MaxInviteeEmailLength);
            SetName(invitee.Name);
        }
    }

    /// <summary>An external guest's name (optional); a colleague has none stored.</summary>
    internal void SetName(string? name)
    {
        Name = UserId is not null || string.IsNullOrWhiteSpace(name)
            ? null
            : Check.Length(name.Trim(), nameof(name), BookingConsts.MaxInviteeNameLength);
    }

    public Invitee ToInvitee() => new(UserId, Email, Name);

    /// <summary>Records their answer. Only Accepted or Declined: nobody answers "pending".</summary>
    internal void Respond(InviteeResponseStatus status, DateTimeOffset now)
    {
        if (status == InviteeResponseStatus.Pending)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "An answer is Accepted or Declined.");
        }

        ResponseStatus = status;
        RespondedAt = now;
    }
}

/// <summary>
/// Someone invited to a <see cref="Booking"/>. Part of the booking aggregate: added and
/// removed only through it.
/// </summary>
public class BookingAttendee : InviteeRow
{
    public Guid BookingId { get; private set; }

    /// <summary>
    /// A copy of the booking's end, so "bookings I'm invited to between two times" seeks an
    /// index on (UserId, EndsAt) instead of reading every invitation the person ever had.
    /// A booking's times never change after it's made; if that ever changes, so must this.
    /// </summary>
    public DateTimeOffset EndsAt { get; private set; }

    private BookingAttendee()
    {
        // EF Core
    }

    internal BookingAttendee(Guid id, Guid bookingId, DateTimeOffset endsAt, Invitee invitee)
        : base(id, invitee)
    {
        BookingId = bookingId;
        EndsAt = endsAt;
    }
}

/// <summary>
/// Someone invited to a whole <see cref="BookingSeries"/>: the list each of its dates is
/// given a copy of. Part of the series aggregate.
/// </summary>
public class BookingSeriesAttendee : InviteeRow
{
    public Guid SeriesId { get; private set; }

    private BookingSeriesAttendee()
    {
        // EF Core
    }

    internal BookingSeriesAttendee(Guid id, Guid seriesId, Invitee invitee)
        : base(id, invitee)
    {
        SeriesId = seriesId;
    }
}

internal static class InviteeRowListExtensions
{
    /// <summary>
    /// Makes the list exactly <paramref name="invitees"/> (already checked, no duplicates): a
    /// person already on it keeps their row (an external's name is updated), the rest are
    /// added or removed. Returns who was added and who was removed.
    /// </summary>
    public static (List<Invitee> Added, List<Invitee> Removed) Replace<T>(
        this List<T> rows, IReadOnlyCollection<Invitee> invitees, Func<Invitee, T> create)
        where T : InviteeRow
    {
        var wanted = invitees.ToDictionary(i => i.Key);

        var removed = rows.Where(r => !wanted.ContainsKey(r.ToInvitee().Key)).ToList();
        foreach (var row in removed)
        {
            rows.Remove(row);
        }

        foreach (var row in rows)
        {
            row.SetName(wanted[row.ToInvitee().Key].Name);
        }

        var kept = rows.Select(r => r.ToInvitee().Key).ToHashSet();
        var added = invitees.Where(i => !kept.Contains(i.Key)).ToList();
        rows.AddRange(added.Select(create));

        return (added, removed.Select(r => r.ToInvitee()).ToList());
    }
}
