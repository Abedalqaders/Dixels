using System;

namespace Dixels.Users;

public class AssignUserBuildingDto
{
    /// <summary>The building they may book in from now on; null to unassign.</summary>
    public Guid? BuildingId { get; set; }

    /// <summary>
    /// Also cancel their upcoming bookings in the building they're leaving (see the reassign
    /// impact endpoint). Left false, they keep them — they can still use or cancel them.
    /// </summary>
    public bool CancelUpcomingBookings { get; set; }
}
