using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// A floor within a <see cref="Building"/>. Every constraint column here is nullable — null
/// means "inherit from the Building" — except <see cref="MaxDurationMinutes"/>-style policy
/// overrides and the days/hours narrow-only rule, both enforced by this class's own setters.
/// </summary>
public class Floor : FullAuditedAggregateRoot<Guid>
{
    public Guid BuildingId { get; private set; }
    public string Name { get; private set; } = null!;
    public int? FloorNumber { get; private set; }
    public OperatingDays? Days { get; private set; }
    public OperatingWindow? Hours { get; private set; }
    public int? MaxDurationMinutes { get; private set; }

    /// <summary>
    /// Set when this floor is soft-deleted as part of a cascading delete — see
    /// <see cref="SpaceHierarchyManager"/>.
    /// </summary>
    public Guid? DeletionBatchId { get; internal set; }

    /// <summary>Clears this floor's own batch id when it's the entity being directly
    /// restored by id — see <see cref="Building.ClearDeletionBatch"/> for why this can't
    /// just be a public setter.</summary>
    public void ClearDeletionBatch()
    {
        DeletionBatchId = null;
    }

    private Floor()
    {
        // EF Core
    }

    public Floor(Guid id, Guid buildingId, string name, int? floorNumber)
        : base(id)
    {
        BuildingId = buildingId;
        SetName(name);
        FloorNumber = floorNumber;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), FloorConsts.MaxNameLength);
    }

    public void SetFloorNumber(int? floorNumber)
    {
        FloorNumber = floorNumber;
    }

    /// <summary>
    /// Sets this floor's own operating-days override, or clears it to inherit (null).
    /// <paramref name="resolvedParentDays"/> must be the Building's currently resolved days.
    /// This check runs only when this floor's own override is written — it is not
    /// re-validated if the Building's days are tightened afterwards; see
    /// <c>ConstraintResolver.FindNarrowingConflicts</c> for that case.
    /// </summary>
    public void SetOwnOperatingDays(OperatingDays? days, OperatingDays resolvedParentDays)
    {
        if (days is not null && !days.IsSubsetOf(resolvedParentDays))
        {
            throw new BusinessException(DixelsDomainErrorCodes.DaysNotNarrower);
        }

        Days = days;
    }

    /// <summary>
    /// Sets this floor's own operating-hours override, or clears it to inherit (null).
    /// <paramref name="resolvedParentHours"/> must be the Building's currently resolved hours.
    /// </summary>
    public void SetOwnOperatingHours(OperatingWindow? hours, OperatingWindow resolvedParentHours)
    {
        if (hours is not null && !hours.IsSubsetOf(resolvedParentHours))
        {
            throw new BusinessException(DixelsDomainErrorCodes.HoursNotNarrower);
        }

        Hours = hours;
    }

    /// <summary>
    /// Maximum duration is booking policy, not physical access, so — unlike days/hours —
    /// it's a plain override with no narrow-only check against the parent.
    /// </summary>
    public void SetOwnMaxDuration(int? maxDurationMinutes)
    {
        if (maxDurationMinutes is <= 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.MaxDurationMustBePositive);
        }

        MaxDurationMinutes = maxDurationMinutes;
    }
}
