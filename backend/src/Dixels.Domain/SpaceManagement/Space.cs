using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// A bookable space on a <see cref="Floor"/>. Narrowing for days/hours is validated against
/// the *resolved* Floor value (the Floor's own override if set, else the Building's),
/// never the raw Floor row.
/// </summary>
public class Space : FullAuditedAggregateRoot<Guid>
{
    public const int MaxNameLength = 128;

    public Guid FloorId { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid SpaceTypeId { get; private set; }
    public int Capacity { get; private set; }
    public OperatingDays? Days { get; private set; }
    public OperatingWindow? Hours { get; private set; }
    public int? MaxDurationMinutes { get; private set; }
    public int? MinAttendees { get; private set; }

    /// <summary>
    /// Set when this space is soft-deleted as part of a cascading delete — see
    /// <see cref="SpaceHierarchyManager"/>.
    /// </summary>
    public Guid? DeletionBatchId { get; internal set; }

    private Space()
    {
        // EF Core
    }

    public Space(Guid id, Guid floorId, string name, Guid spaceTypeId, int capacity)
        : base(id)
    {
        FloorId = floorId;
        SetName(name);
        SpaceTypeId = spaceTypeId;
        SetCapacity(capacity);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MaxNameLength);
    }

    public void SetSpaceType(Guid spaceTypeId)
    {
        SpaceTypeId = spaceTypeId;
    }

    /// <summary>
    /// Lowering capacity below the current minimum group size raises a specific error
    /// naming both numbers, rather than a generic constraint-violation message — an admin
    /// needs to know exactly what to fix.
    /// </summary>
    public void SetCapacity(int capacity)
    {
        if (capacity <= 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.CapacityMustBePositive);
        }

        if (MinAttendees is not null && capacity < MinAttendees.Value)
        {
            throw new BusinessException(DixelsDomainErrorCodes.CapacityBelowMinAttendees)
                .WithData("capacity", capacity)
                .WithData("minAttendees", MinAttendees.Value);
        }

        Capacity = capacity;
    }

    public void SetOwnOperatingDays(OperatingDays? days, OperatingDays resolvedParentDays)
    {
        if (days is not null && !days.IsSubsetOf(resolvedParentDays))
        {
            throw new BusinessException(DixelsDomainErrorCodes.DaysNotNarrower);
        }

        Days = days;
    }

    public void SetOwnOperatingHours(OperatingWindow? hours, OperatingWindow resolvedParentHours)
    {
        if (hours is not null && !hours.IsSubsetOf(resolvedParentHours))
        {
            throw new BusinessException(DixelsDomainErrorCodes.HoursNotNarrower);
        }

        Hours = hours;
    }

    public void SetOwnMaxDuration(int? maxDurationMinutes)
    {
        if (maxDurationMinutes is <= 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.MaxDurationMustBePositive);
        }

        MaxDurationMinutes = maxDurationMinutes;
    }

    /// <summary>
    /// Anti-waste rule: null means no minimum. A minimum above the current capacity would
    /// make this space permanently unbookable, so it's rejected with both numbers named.
    /// </summary>
    public void SetMinAttendees(int? minAttendees)
    {
        if (minAttendees is not null)
        {
            if (minAttendees.Value <= 0)
            {
                throw new BusinessException(DixelsDomainErrorCodes.MinAttendeesMustBePositive);
            }

            if (minAttendees.Value > Capacity)
            {
                throw new BusinessException(DixelsDomainErrorCodes.MinAttendeesExceedsCapacity)
                    .WithData("minAttendees", minAttendees.Value)
                    .WithData("capacity", Capacity);
            }
        }

        MinAttendees = minAttendees;
    }
}
