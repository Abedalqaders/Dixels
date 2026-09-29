using System;
using System.Collections.Generic;

namespace Dixels.Users;

/// <summary>One user's role names, for the Users page's Roles column — a batch call so the
/// page doesn't fire one request per row.</summary>
public class UserRolesDto
{
    public Guid UserId { get; set; }

    public List<string> Roles { get; set; } = new();
}
