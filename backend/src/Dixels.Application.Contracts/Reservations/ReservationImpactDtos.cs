using System;
using System.Collections.Generic;
using Volo.Abp.Timing;

namespace Dixels.Reservations;

/// <summary>The kinds of reservation a module can hold in a room — one per module that holds them.</summary>
public static class ReservationKinds
{
    public const string Booking = "booking";
}

/// <summary>
/// One upcoming reservation an admin's change would affect, as the admin sees it. Generic on
/// purpose: a booking today, a parking spot or a visit later — <see cref="Kind"/> says which.
/// </summary>
public class AffectedReservationDto
{
    /// <summary>Which module holds it (<see cref="ReservationKinds"/>).</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Who holds it (name, or user name when there's no name).</summary>
    public string HeldBy { get; set; } = string.Empty;

    /// <summary>Where: the room (or spot) itself.</summary>
    public string PlaceName { get; set; } = string.Empty;

    /// <summary>Where, more broadly: the floor it's on.</summary>
    public string PlaceDetail { get; set; } = string.Empty;

    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }

    /// <summary>Why it no longer fits, in a few words each ("Open 09:00–17:00 only").</summary>
    public List<string> Reasons { get; set; } = new();
}

/// <summary>
/// "This change affects N upcoming reservations" — shown before an admin saves, closes,
/// deletes or moves someone. Gathered from every module that holds reservations.
/// </summary>
public class ReservationImpactDto
{
    public int Count { get; set; }
    public List<AffectedReservationDto> Items { get; set; } = new();

    /// <summary>Deleting a building: how many employees are assigned to it (they can't book until reassigned).</summary>
    public int AssignedEmployees { get; set; }
}
