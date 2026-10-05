using System;

namespace Dixels.Users;

// What happens to people's accounts, announced on ABP's local event bus (ILocalEventHandler<T>
// to listen; BookingUserEventHandler in Bookings is one). User management only announces —
// it doesn't know which modules hold something for a person (bookings today; parking or
// visitors later), and each of those listens and tidies up its own.
//
// Listeners run inside the account change's own transaction, when it saves. So a listener
// must be quick, and an exception rolls the account change back with it — which is what we
// want here: an account isn't deactivated with its bookings still holding rooms.

/// <summary>An active account was deactivated: nobody will turn up for what it holds.</summary>
public class UserDeactivatedEvent
{
    public Guid UserId { get; }

    /// <summary>The admin who did it.</summary>
    public Guid ByUserId { get; }

    public UserDeactivatedEvent(Guid userId, Guid byUserId)
    {
        UserId = userId;
        ByUserId = byUserId;
    }
}

/// <summary>An account was removed.</summary>
public class UserDeletedEvent
{
    public Guid UserId { get; }

    /// <summary>The admin who did it.</summary>
    public Guid ByUserId { get; }

    public UserDeletedEvent(Guid userId, Guid byUserId)
    {
        UserId = userId;
        ByUserId = byUserId;
    }
}

/// <summary>
/// Someone was moved to another building, or unassigned (<see cref="ToBuildingId"/> null).
/// Raised only when the building really changed.
/// </summary>
public class UserMovedBuildingEvent
{
    public Guid UserId { get; }
    public Guid? FromBuildingId { get; }
    public Guid? ToBuildingId { get; }

    /// <summary>The admin who did it.</summary>
    public Guid ByUserId { get; }

    public UserMovedBuildingEvent(Guid userId, Guid? fromBuildingId, Guid? toBuildingId, Guid byUserId)
    {
        UserId = userId;
        FromBuildingId = fromBuildingId;
        ToBuildingId = toBuildingId;
        ByUserId = byUserId;
    }
}
