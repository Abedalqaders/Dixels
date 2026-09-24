using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// Stateless resolution of the Building → Floor → Space constraint hierarchy: a
/// COALESCE-equivalent, per-field pick (space wins, then floor, then building), with
/// provenance. This is the single implementation used both when reading (the constraints
/// page's resolved-values strip) and when writing (narrowing validation on save).
/// </summary>
public class ConstraintResolver : IDomainService
{
    public ResolvedConstraints Resolve(Building building, Floor floor, Space? space = null)
    {
        var days = Pick(space?.Days, floor.Days, building.Days);
        var hours = Pick(space?.Hours, floor.Hours, building.Hours);
        var maxDurationMinutes = Pick(space?.MaxDurationMinutes, floor.MaxDurationMinutes, building.MaxDurationMinutes);

        return new ResolvedConstraints(
            building.Timezone,
            days,
            hours,
            maxDurationMinutes,
            building.MaxHorizonDays,
            building.MinLeadMinutes,
            space?.MinAttendees,
            space?.Capacity);
    }

    public void EnsureDaysNarrowing(OperatingDays? childOwn, OperatingDays resolvedParent)
    {
        if (childOwn is not null && !childOwn.IsSubsetOf(resolvedParent))
        {
            throw new BusinessException(DixelsDomainErrorCodes.DaysNotNarrower);
        }
    }

    public void EnsureHoursNarrowing(OperatingWindow? childOwn, OperatingWindow resolvedParent)
    {
        if (childOwn is not null && !childOwn.IsSubsetOf(resolvedParent))
        {
            throw new BusinessException(DixelsDomainErrorCodes.HoursNotNarrower);
        }
    }

    /// <summary>
    /// Given the descendants that have their own days/hours override set, returns the
    /// display names of the ones that would no longer fit under a proposed, tighter parent
    /// value. This is a non-blocking, informational check — the save that tightens the
    /// parent still proceeds (tightening never retroactively invalidates), but the admin
    /// sees exactly which descendants now have a stale override.
    /// </summary>
    public IReadOnlyList<string> FindNarrowingConflicts(
        IEnumerable<NarrowingCandidate> candidates,
        OperatingDays? proposedDays,
        OperatingWindow? proposedHours)
    {
        var conflicts = new List<string>();

        foreach (var candidate in candidates)
        {
            if (proposedDays is not null && candidate.OwnDays is not null && !candidate.OwnDays.IsSubsetOf(proposedDays))
            {
                conflicts.Add($"{candidate.DisplayName} no longer fits inside the new operating days");
            }

            if (proposedHours is not null && candidate.OwnHours is not null && !candidate.OwnHours.IsSubsetOf(proposedHours))
            {
                conflicts.Add($"{candidate.DisplayName} no longer fits inside the new operating hours");
            }
        }

        return conflicts;
    }

    private static FieldValue<T> Pick<T>(T? spaceValue, T? floorValue, T building)
        where T : class
    {
        if (spaceValue is not null)
        {
            return new FieldValue<T>(spaceValue, ConstraintSource.Space);
        }

        if (floorValue is not null)
        {
            return new FieldValue<T>(floorValue, ConstraintSource.Floor);
        }

        return new FieldValue<T>(building, ConstraintSource.Building);
    }

    private static FieldValue<int> Pick(int? spaceValue, int? floorValue, int building)
    {
        if (spaceValue.HasValue)
        {
            return new FieldValue<int>(spaceValue.Value, ConstraintSource.Space);
        }

        if (floorValue.HasValue)
        {
            return new FieldValue<int>(floorValue.Value, ConstraintSource.Floor);
        }

        return new FieldValue<int>(building, ConstraintSource.Building);
    }
}
