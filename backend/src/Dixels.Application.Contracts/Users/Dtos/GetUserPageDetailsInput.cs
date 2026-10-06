using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Users;

public class GetUserPageDetailsInput
{
    /// <summary>The largest page the Users list offers.</summary>
    public const int MaxUserIds = 100;

    /// <summary>One page's users — capped, so this is no way around the list's paging.</summary>
    [MaxLength(MaxUserIds)]
    public List<Guid> UserIds { get; set; } = new();
}
