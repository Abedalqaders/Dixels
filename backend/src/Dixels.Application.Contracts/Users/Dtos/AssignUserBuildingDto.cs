using System;

namespace Dixels.Users;

public class AssignUserBuildingDto
{
    /// <summary>
    /// The building they may book in from now on; null to unassign. Their upcoming bookings
    /// in the building they leave are cancelled (see the reassign impact endpoint).
    /// </summary>
    public Guid? BuildingId { get; set; }
}
