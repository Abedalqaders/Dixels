using System;
using System.Collections.Generic;

namespace Dixels.SpaceManagement;

// What happens to buildings, floors and rooms, announced on ABP's local event bus
// (ILocalEventHandler<T> to listen; BookingSpaceEventHandler in Bookings is one). Space
// management only announces — it doesn't know which modules hold something in a room
// (bookings today; parking or visitors later), and each of those listens and tidies up its own.
//
// Each event lists every room it reaches (SpaceIds), so a listener can find what it holds
// there without querying space management for it (for a delete: rooms already deleted).
//
// Listeners run inside the change's own transaction, when it saves. So a listener must be
// quick, and an exception rolls the change back with it: work that can be large (releasing
// everything a deleted building held) belongs in a background job the listener queues.

/// <summary>A room was deleted.</summary>
public class SpaceDeletedEvent
{
    public Guid SpaceId { get; }
    public IReadOnlyList<Guid> SpaceIds { get; }

    /// <summary>The admin who did it.</summary>
    public Guid ByUserId { get; }

    public SpaceDeletedEvent(Guid spaceId, Guid byUserId)
    {
        SpaceId = spaceId;
        SpaceIds = new[] { spaceId };
        ByUserId = byUserId;
    }
}

/// <summary>A floor was deleted, with every room on it.</summary>
public class FloorDeletedEvent
{
    public Guid FloorId { get; }
    public IReadOnlyList<Guid> SpaceIds { get; }

    /// <summary>The admin who did it.</summary>
    public Guid ByUserId { get; }

    public FloorDeletedEvent(Guid floorId, IReadOnlyList<Guid> spaceIds, Guid byUserId)
    {
        FloorId = floorId;
        SpaceIds = spaceIds;
        ByUserId = byUserId;
    }
}

/// <summary>A building was deleted, with every floor and room in it.</summary>
public class BuildingDeletedEvent
{
    public Guid BuildingId { get; }
    public IReadOnlyList<Guid> SpaceIds { get; }

    /// <summary>The admin who did it.</summary>
    public Guid ByUserId { get; }

    public BuildingDeletedEvent(Guid buildingId, IReadOnlyList<Guid> spaceIds, Guid byUserId)
    {
        BuildingId = buildingId;
        SpaceIds = spaceIds;
        ByUserId = byUserId;
    }
}

/// <summary>
/// The rules of some rooms changed — a building's, floor's or room's opening days and hours,
/// longest booking or smallest group, or a room's capacity — and are saved. Lists every room
/// the change reaches. <see cref="CancelAffected"/> is the admin's choice after seeing the
/// preview: true to cancel what no longer fits, false to keep it.
/// </summary>
public class SpaceRulesChangedEvent
{
    public Guid BuildingId { get; }
    public IReadOnlyList<Guid> SpaceIds { get; }
    public bool CancelAffected { get; }

    /// <summary>
    /// What the change was already found to break, worked out once before saving (see
    /// <see cref="AffectedReservation"/>); null when it wasn't, and each listener checks itself.
    /// </summary>
    public IReadOnlyList<AffectedReservation>? Affected { get; }

    /// <summary>The admin who did it.</summary>
    public Guid ByUserId { get; }

    public SpaceRulesChangedEvent(
        Guid buildingId,
        IReadOnlyList<Guid> spaceIds,
        bool cancelAffected,
        Guid byUserId,
        IReadOnlyList<AffectedReservation>? affected = null)
    {
        BuildingId = buildingId;
        SpaceIds = spaceIds;
        CancelAffected = cancelAffected;
        ByUserId = byUserId;
        Affected = affected;
    }
}

/// <summary>
/// A closure was added (saved) over some rooms. Lists every room it reaches, and the reason
/// people are shown ("Closed: HVAC maintenance"). <see cref="CancelAffected"/> is the admin's
/// choice after seeing the preview. A special opening raises nothing: it can't break anything.
/// </summary>
public class ClosureCreatedEvent
{
    public Guid ClosureId { get; }
    public Guid BuildingId { get; }
    public IReadOnlyList<Guid> SpaceIds { get; }
    public string Reason { get; }
    public bool CancelAffected { get; }

    /// <summary>What the closure was already found to break, before saving; null: each listener checks itself.</summary>
    public IReadOnlyList<AffectedReservation>? Affected { get; }

    /// <summary>The admin who did it.</summary>
    public Guid ByUserId { get; }

    public ClosureCreatedEvent(
        Guid closureId,
        Guid buildingId,
        IReadOnlyList<Guid> spaceIds,
        string reason,
        bool cancelAffected,
        Guid byUserId,
        IReadOnlyList<AffectedReservation>? affected = null)
    {
        ClosureId = closureId;
        BuildingId = buildingId;
        SpaceIds = spaceIds;
        Reason = reason;
        CancelAffected = cancelAffected;
        ByUserId = byUserId;
        Affected = affected;
    }
}

/// <summary>
/// One reservation a change breaks, as the preview found it on the unsaved change in the same
/// request. A save that has just worked this out (to tell the admin how many it cancels)
/// passes it along, so the module holding it releases exactly these instead of checking every
/// upcoming reservation again. <see cref="Kind"/> says which module holds it; each module
/// skips the others'. <see cref="Reason"/> is the first rule it breaks, in a few words.
/// </summary>
public sealed record AffectedReservation(string Kind, Guid Id, string Reason);
