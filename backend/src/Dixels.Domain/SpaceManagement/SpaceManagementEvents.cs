using System;
using System.Collections.Generic;

namespace Dixels.SpaceManagement;

// What happens to buildings, floors and rooms, announced on ABP's local event bus
// (ILocalEventHandler<T> to listen; BookingSpaceEventHandler in Bookings is one). Space
// management only announces — it doesn't know which modules hold something in a room
// (bookings today; parking or visitors later), and each of those listens and tidies up its own.
//
// Each delete event lists every room that went with it (SpaceIds), so a listener can find
// what it holds there without querying rooms that are already deleted.
//
// Listeners run inside the delete's own transaction, when it saves. So a listener must be
// quick, and an exception rolls the delete back with it.

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
