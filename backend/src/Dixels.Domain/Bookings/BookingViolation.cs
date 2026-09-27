using System.Collections.Generic;
using Dixels.SpaceManagement;

namespace Dixels.Bookings;

/// <summary>
/// One broken booking rule. <see cref="Code"/> is a localization key
/// (<see cref="DixelsDomainErrorCodes"/>); <see cref="Data"/> fills that message's
/// placeholders with the real limit and the requested value; <see cref="Level"/> is which
/// level of the hierarchy set the rule (read from the resolved value's provenance, never
/// hard-coded per rule) — null for rules that don't belong to a level, like slot alignment.
/// </summary>
public sealed record BookingViolation(string Code, ConstraintSource? Level, IReadOnlyDictionary<string, object> Data);
