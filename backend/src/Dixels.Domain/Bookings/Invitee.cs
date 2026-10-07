using System;

namespace Dixels.Bookings;

/// <summary>
/// One person invited to a booking: a colleague (<see cref="UserId"/>) or an external guest
/// (<see cref="Email"/>, with an optional <see cref="Name"/>). As asked for, both may be set or
/// neither; once <see cref="BookingInviteeResolver"/> has checked it, exactly one is, and a
/// colleague's Email/Name are their current details (for showing, never stored).
/// </summary>
public sealed record Invitee(Guid? UserId, string? Email, string? Name)
{
    public bool IsExternal => UserId is null;

    /// <summary>
    /// Who this is, for "the same person twice": the user id, or the email trimmed and
    /// upper-cased (emails compare case-insensitively). A colleague's email isn't part of it.
    /// </summary>
    public string Key => UserId?.ToString() ?? NormalizeEmail(Email!);

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
