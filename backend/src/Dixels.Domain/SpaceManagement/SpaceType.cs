using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.SpaceManagement;

/// <summary>
/// An admin-configurable kind of bookable space (e.g. "Meeting room", "Focus pod", "Desk").
/// Company-wide, not scoped to a building. Deleting a type that's still referenced by a
/// <see cref="Space"/> is rejected by the application layer, not cascaded — a lookup value
/// in use shouldn't silently vanish out from under existing spaces.
/// </summary>
public class SpaceType : FullAuditedAggregateRoot<Guid>
{
    public const int MaxNameLength = 128;

    public string Name { get; private set; } = null!;
    public IconKey IconKey { get; private set; }

    private SpaceType()
    {
        // EF Core
    }

    public SpaceType(Guid id, string name, IconKey iconKey = IconKey.Generic)
        : base(id)
    {
        SetName(name);
        IconKey = iconKey;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MaxNameLength);
    }

    public void SetIconKey(IconKey iconKey)
    {
        IconKey = iconKey;
    }
}
