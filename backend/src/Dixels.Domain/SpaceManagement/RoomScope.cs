using System;

namespace Dixels.SpaceManagement;

/// <summary>
/// "The rooms in…": a whole building, one of its floors, or a single room — named by id, so a
/// query can find the rooms itself instead of loading them first.
/// </summary>
public sealed record RoomScope(Guid BuildingId, Guid? FloorId = null, Guid? SpaceId = null);
