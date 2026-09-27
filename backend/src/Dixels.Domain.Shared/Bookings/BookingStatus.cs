namespace Dixels.Bookings;

/// <summary>
/// The stored lifecycle state of a booking. <c>Completed</c> (from the BRS state model) is
/// deliberately not stored — it's derived as "Confirmed and the end time has passed", so no
/// background job has to flip rows at the right moment and nothing can ever disagree with
/// the clock. <see cref="Expired"/> is reserved for temporary holds, which no flow creates yet.
/// </summary>
public enum BookingStatus
{
    Confirmed,
    Cancelled,
    Expired
}
