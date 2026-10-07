using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiLingualObjects;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// The base layer of the Building → Floor → Space hierarchy. Every constraint column here
/// is required (non-null) — this guarantees resolution always terminates with a real value,
/// so the resolver never has to handle "no value set anywhere".
/// </summary>
public class Building : FullAuditedAggregateRoot<Guid>, IMultiLingualObject<BuildingTranslation>
{
    /// <summary>
    /// Its name, once per language (ABP MultiLingualObjects) — there's no single Name. Which
    /// one a reader sees is picked by IMultiLingualObjectManager: their language, else the
    /// default language's (always there: it's required). Settable only because ABP's
    /// interface demands it; use SetName/SetNames.
    /// </summary>
    public ICollection<BuildingTranslation> Translations { get; set; } = new List<BuildingTranslation>();

    public string? BuildingNumber { get; private set; }
    public string Timezone { get; private set; } = null!;
    public OperatingDays Days { get; private set; } = null!;
    public OperatingWindow Hours { get; private set; } = null!;
    public int MaxDurationMinutes { get; private set; }
    public int MaxHorizonDays { get; private set; }

    /// <summary>How far ahead a recurring booking's dates may run — never shorter than <see cref="MaxHorizonDays"/>.</summary>
    public int MaxSeriesHorizonDays { get; private set; }
    public int MinLeadMinutes { get; private set; }

    /// <summary>Whether one person may hold two bookings at the same time here.</summary>
    public OwnOverlapPolicy OwnOverlapPolicy { get; private set; }

    /// <summary>
    /// Set when this building is soft-deleted as part of a cascading delete, so a later
    /// restore can be scoped to exactly what was deleted together — see
    /// <see cref="SpaceHierarchyManager"/>.
    /// </summary>
    public Guid? DeletionBatchId { get; internal set; }

    /// <summary>
    /// Clears this building's own batch id when it's the entity being directly restored by
    /// id — not a descendant, which <see cref="SpaceHierarchyManager.SelectAndClearForRestore(Guid,System.Collections.Generic.IReadOnlyList{Floor})"/>
    /// already handles (batch-scoped, since a descendant could've been deleted independently).
    /// The root of a restore needs no such scoping — the caller already knows exactly which
    /// building it asked to restore.
    /// </summary>
    public void ClearDeletionBatch()
    {
        DeletionBatchId = null;
    }

    private Building()
    {
        // EF Core
    }

    public Building(
        Guid id,
        string language,
        string name,
        string? buildingNumber,
        string timezone,
        OperatingDays days,
        OperatingWindow hours,
        int maxDurationMinutes,
        int maxHorizonDays,
        int minLeadMinutes,
        OwnOverlapPolicy ownOverlapPolicy = OwnOverlapPolicy.Warn,
        int? maxSeriesHorizonDays = null)
        : base(id)
    {
        SetName(language, name);
        SetBuildingNumber(buildingNumber);
        SetTimezone(timezone);
        Days = Check.NotNull(days, nameof(days));
        Hours = Check.NotNull(hours, nameof(hours));
        SetMaxDurationMinutes(maxDurationMinutes);
        SetMaxHorizonDays(maxHorizonDays);
        SetMaxSeriesHorizonDays(maxSeriesHorizonDays ?? Math.Max(DefaultMaxSeriesHorizonDays, maxHorizonDays));
        SetMinLeadMinutes(minLeadMinutes);
        SetOwnOverlapPolicy(ownOverlapPolicy);
    }

    /// <summary>The name in exactly this language, if it has one (no fallback).</summary>
    public string? FindName(string language) => Translations.FindName(language);

    public void SetName(string language, string name) => Translations.SetName(language, name, NewTranslation);

    public void RemoveName(string language) => Translations.RemoveName(language);

    /// <summary>Makes the names exactly these (validated by LocalizedNameValidator): a language left out loses its name.</summary>
    public void SetNames(IReadOnlyCollection<LocalizedName> names) => Translations.SetNames(names, NewTranslation);

    private BuildingTranslation NewTranslation(string language, string name) => new(Id, language, name);

    /// <summary>The address in exactly this language, if it has one (no fallback).</summary>
    public string? FindAddress(string language) => Translations.FirstOrDefault(t => t.Language == language)?.Address;

    /// <summary>
    /// Sets each language's address (blank clears it). Call after <see cref="SetNames"/>: an
    /// address lives on its language's name row, so it's only kept for a language that has a name.
    /// </summary>
    public void SetAddresses(IReadOnlyDictionary<string, string?> addressByLanguage)
    {
        foreach (var translation in Translations)
        {
            translation.SetAddress(addressByLanguage.GetValueOrDefault(translation.Language));
        }
    }

    public void SetBuildingNumber(string? buildingNumber)
    {
        BuildingNumber = Check.Length(buildingNumber, nameof(buildingNumber), BuildingConsts.MaxBuildingNumberLength);
    }

    /// <summary>
    /// Validates against the real IANA timezone database (via
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>) rather than accepting arbitrary text.
    /// </summary>
    public void SetTimezone(string timezone)
    {
        Check.NotNullOrWhiteSpace(timezone, nameof(timezone), BuildingConsts.MaxTimezoneLength);

        if (!IsValidIanaTimezone(timezone))
        {
            throw new BusinessException(DixelsDomainErrorCodes.InvalidTimezone)
                .WithData("timezone", timezone);
        }

        Timezone = timezone;
    }

    public void SetOperatingDays(OperatingDays days)
    {
        Days = Check.NotNull(days, nameof(days));
    }

    public void SetOperatingHours(OperatingWindow hours)
    {
        Hours = Check.NotNull(hours, nameof(hours));
    }

    public void SetMaxDurationMinutes(int maxDurationMinutes)
    {
        if (maxDurationMinutes <= 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.MaxDurationMustBePositive);
        }

        MaxDurationMinutes = maxDurationMinutes;
    }

    public void SetMaxHorizonDays(int maxHorizonDays)
    {
        if (maxHorizonDays <= 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.MaxHorizonDaysMustBePositive);
        }

        MaxHorizonDays = maxHorizonDays;

        // The series horizon can never be shorter: moving the normal one past it pulls it along.
        if (MaxSeriesHorizonDays < maxHorizonDays)
        {
            MaxSeriesHorizonDays = maxHorizonDays;
        }
    }

    /// <summary>Used when a building is created without its own series horizon.</summary>
    public const int DefaultMaxSeriesHorizonDays = 90;

    public void SetMaxSeriesHorizonDays(int maxSeriesHorizonDays)
    {
        if (maxSeriesHorizonDays < MaxHorizonDays)
        {
            throw new BusinessException(DixelsDomainErrorCodes.MaxSeriesHorizonTooShort)
                .WithData("horizonDays", MaxHorizonDays);
        }

        MaxSeriesHorizonDays = maxSeriesHorizonDays;
    }

    public void SetMinLeadMinutes(int minLeadMinutes)
    {
        if (minLeadMinutes < 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.MinLeadMinutesMustNotBeNegative);
        }

        MinLeadMinutes = minLeadMinutes;
    }

    public void SetOwnOverlapPolicy(OwnOverlapPolicy policy)
    {
        if (!Enum.IsDefined(policy))
        {
            throw new BusinessException(DixelsDomainErrorCodes.InvalidOwnOverlapPolicy);
        }

        OwnOverlapPolicy = policy;
    }

    private static bool IsValidIanaTimezone(string timezone)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
