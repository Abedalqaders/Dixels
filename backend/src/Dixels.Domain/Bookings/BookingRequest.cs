using System;

namespace Dixels.Bookings;

/// <summary>The time window and head count being validated, already converted to UTC.</summary>
public sealed record BookingRequest(DateTimeOffset StartUtc, DateTimeOffset EndUtc, int Attendees);
