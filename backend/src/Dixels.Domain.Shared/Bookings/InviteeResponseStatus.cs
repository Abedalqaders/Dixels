namespace Dixels.Bookings;

/// <summary>An invitee's answer to the invitation. Everyone starts Pending; answering comes later.</summary>
public enum InviteeResponseStatus
{
    Pending,
    Accepted,
    Declined
}
