using System;
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
        var days = Pick(
            (space?.Days, ConstraintSource.Space),
            (floor.Days, ConstraintSource.Floor),
            (building.Days, ConstraintSource.Building));
        var hours = Pick(
            (space?.Hours, ConstraintSource.Space),
            (floor.Hours, ConstraintSource.Floor),
            (building.Hours, ConstraintSource.Building));
        var maxDurationMinutes = Pick(
            (space?.MaxDurationMinutes, ConstraintSource.Space),
            (floor.MaxDurationMinutes, ConstraintSource.Floor),
            (building.MaxDurationMinutes, ConstraintSource.Building));

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

    // Levels are walked in the order given — the first non-null value wins. Adding a level to
    // the hierarchy (e.g. a "Wing" between Building and Floor) means adding one more tuple at
    // each call site above; this method itself never needs to change (Open/Closed).
    private static FieldValue<T> Pick<T>(params (T? Value, ConstraintSource Source)[] levels)
        where T : class
    {
        foreach (var (value, source) in levels)
        {
            if (value is not null)
            {
                return new FieldValue<T>(value, source);
            }
        }

        throw new InvalidOperationException("The last level in the chain must always supply a non-null value.");
    }

    // Separate overload for value-typed fields (int) — C# can't express "T? that's either a
    // nullable reference or a Nullable<T> value type" with one unconstrained generic method, so
    // this duplication is a language limitation, not a hierarchy-precedence rule living in two
    // places.
    private static FieldValue<int> Pick(params (int? Value, ConstraintSource Source)[] levels)
    {
        foreach (var (value, source) in levels)
        {
            if (value.HasValue)
            {
                return new FieldValue<int>(value.Value, source);
            }
        }

        throw new InvalidOperationException("The last level in the chain must always supply a non-null value.");
    }
}
