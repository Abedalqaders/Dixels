using System;
using System.Collections.Generic;

namespace Dixels.Users;

/// <summary>
/// What a row of the Users page shows beyond ABP's user: the building's name and the roles —
/// one batch call for the page, so it fires neither one request per row nor one per building.
/// </summary>
public class UserPageDetailsDto
{
    public Guid UserId { get; set; }

    /// <summary>The assigned building's name, in the reader's language; null when unassigned.</summary>
    public string? BuildingName { get; set; }

    /// <summary>The assigned building was deleted since (or can't be found): the page shows
    /// them as needing a new one, with <see cref="BuildingName"/> — its last name — when known.</summary>
    public bool BuildingRemoved { get; set; }

    public List<string> Roles { get; set; } = new();
}
