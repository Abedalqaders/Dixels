namespace Dixels.Bookings;

/// <summary>
/// An invitee's answer to the invitation. Everyone starts Pending. Maybe ("tentative" in
/// mail apps) came last, so it's numbered last: the API sends these as numbers.
/// </summary>
public enum InviteeResponseStatus
{
    Pending,
    Accepted,
    Declined,
    Maybe
}
