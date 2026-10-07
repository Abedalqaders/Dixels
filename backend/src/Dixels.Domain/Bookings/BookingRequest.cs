using System;

namespace Dixels.Bookings;

/// <summary>
/// The time window and head count being validated, already converted to UTC, and how many
/// people are invited (the head count must leave room for them plus the owner).
/// </summary>
public sealed record BookingRequest(DateTimeOffset StartUtc, DateTimeOffset EndUtc, int Attendees, int Invitees = 0);
