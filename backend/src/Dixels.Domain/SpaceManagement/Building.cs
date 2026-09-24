using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// The base layer of the Building → Floor → Space hierarchy. Every constraint column here
/// is required (non-null) — this guarantees resolution always terminates with a real value,
/// so the resolver never has to handle "no value set anywhere".
/// </summary>
public class Building : FullAuditedAggregateRoot<Guid>
{
    public const int MaxNameLength = 128;
    public const int MaxBuildingNumberLength = 32;
    public const int MaxTimezoneLength = 64;

    public string Name { get; private set; } = null!;
    public string? BuildingNumber { get; private set; }
    public string Timezone { get; private set; } = null!;
    public OperatingDays Days { get; private set; } = null!;
    public OperatingWindow Hours { get; private set; } = null!;
    public int MaxDurationMinutes { get; private set; }
    public int MaxHorizonDays { get; private set; }
    public int MinLeadMinutes { get; private set; }

    /// <summary>
    /// Set when this building is soft-deleted as part of a cascading delete, so a later
    /// restore can be scoped to exactly what was deleted together — see
    /// <see cref="SpaceHierarchyManager"/>.
    /// </summary>
    public Guid? DeletionBatchId { get; internal set; }

    private Building()
    {
        // EF Core
    }

    public Building(
        Guid id,
        string name,
        string? buildingNumber,
        string timezone,
        OperatingDays days,
        OperatingWindow hours,
        int maxDurationMinutes,
        int maxHorizonDays,
        int minLeadMinutes)
        : base(id)
    {
        SetName(name);
        SetBuildingNumber(buildingNumber);
        SetTimezone(timezone);
        Days = Check.NotNull(days, nameof(days));
        Hours = Check.NotNull(hours, nameof(hours));
        SetMaxDurationMinutes(maxDurationMinutes);
        SetMaxHorizonDays(maxHorizonDays);
        SetMinLeadMinutes(minLeadMinutes);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MaxNameLength);
    }

    public void SetBuildingNumber(string? buildingNumber)
    {
        if (buildingNumber is not null && buildingNumber.Length > MaxBuildingNumberLength)
        {
            throw new ArgumentException(
                $"Building number can't be longer than {MaxBuildingNumberLength} characters.",
                nameof(buildingNumber));
        }

        BuildingNumber = buildingNumber;
    }

    /// <summary>
    /// Validates against the real IANA timezone database (via
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>) rather than accepting arbitrary text.
    /// </summary>
    public void SetTimezone(string timezone)
    {
        Check.NotNullOrWhiteSpace(timezone, nameof(timezone), MaxTimezoneLength);

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
    }

    public void SetMinLeadMinutes(int minLeadMinutes)
    {
        if (minLeadMinutes < 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.MinLeadMinutesMustNotBeNegative);
        }

        MinLeadMinutes = minLeadMinutes;
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
