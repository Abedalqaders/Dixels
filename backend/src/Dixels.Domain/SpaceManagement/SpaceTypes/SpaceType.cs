using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiLingualObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// An admin-configurable kind of bookable space (e.g. "Meeting room", "Focus pod", "Desk").
/// Company-wide, not scoped to a building. Deleting a type that's still referenced by a
/// <see cref="Space"/> is rejected by the application layer, not cascaded — a lookup value
/// in use shouldn't silently vanish out from under existing spaces.
///
/// Its name is stored once per language (<see cref="Translations"/>, ABP's
/// MultiLingualObjects): there's no single Name. Which one a reader sees is picked by
/// IMultiLingualObjectManager — their language, else the default language's.
/// </summary>
public class SpaceType : FullAuditedAggregateRoot<Guid>, IMultiLingualObject<SpaceTypeTranslation>
{
    public IconKey IconKey { get; private set; }

    /// <summary>Settable only because ABP's interface demands it; use SetName/RemoveName.</summary>
    public ICollection<SpaceTypeTranslation> Translations { get; set; } = new List<SpaceTypeTranslation>();

    private SpaceType()
    {
        // EF Core
    }

    public SpaceType(Guid id, IconKey iconKey = IconKey.Generic)
        : base(id)
    {
        IconKey = iconKey;
    }

    /// <summary>A type with one name, e.g. a built-in one: <c>new SpaceType(id, "en", "Desk", IconKey.Desk)</c>.</summary>
    public SpaceType(Guid id, string language, string name, IconKey iconKey = IconKey.Generic)
        : this(id, iconKey)
    {
        SetName(language, name);
    }

    /// <summary>The name in exactly this language, if it has one (no fallback).</summary>
    public string? FindName(string language) => Translations.FirstOrDefault(t => t.Language == language)?.Name;

    public void SetName(string language, string name)
    {
        var existing = Translations.FirstOrDefault(t => t.Language == language);
        if (existing is null)
        {
            Translations.Add(new SpaceTypeTranslation(Id, language, name));
        }
        else
        {
            existing.SetName(name);
        }
    }

    public void RemoveName(string language)
    {
        var existing = Translations.FirstOrDefault(t => t.Language == language);
        if (existing is not null)
        {
            Translations.Remove(existing);
        }
    }

    public void SetIconKey(IconKey iconKey)
    {
        IconKey = iconKey;
    }

    /// <summary>
    /// Called just before the type is (soft-)deleted: frees its names for reuse (see
    /// <see cref="SpaceTypeTranslation.IsDeleted"/>).
    /// </summary>
    public void ReleaseNames()
    {
        foreach (var translation in Translations)
        {
            translation.MarkDeleted();
        }
    }
}
