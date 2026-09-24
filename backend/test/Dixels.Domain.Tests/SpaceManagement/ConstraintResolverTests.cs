using System;
using Shouldly;
using Xunit;
using Volo.Abp;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement.Tests;

public class ConstraintResolverTests
{
    private readonly ConstraintResolver _resolver = new();

    private static Building CreateBuilding(
        OperatingDays? days = null,
        OperatingWindow? hours = null,
        int maxDurationMinutes = 480)
        => new(
            Guid.NewGuid(),
            "Ridge House",
            "RH-01",
            "Asia/Amman",
            days ?? OperatingDays.Everyday,
            hours ?? OperatingWindow.FullDay,
            maxDurationMinutes,
            maxHorizonDays: 14,
            minLeadMinutes: 15);

    private static Floor CreateFloor(Guid buildingId, string name = "Level 3")
        => new(Guid.NewGuid(), buildingId, name, floorNumber: 3);

    private static Space CreateSpace(Guid floorId, string name = "Meeting Room 3B", int capacity = 8)
        => new(Guid.NewGuid(), floorId, name, Guid.NewGuid(), capacity);

    [Fact]
    public void Resolve_falls_back_to_building_when_nothing_overrides()
    {
        var building = CreateBuilding();
        var floor = CreateFloor(building.Id);

        var resolved = _resolver.Resolve(building, floor);

        resolved.Days.Source.ShouldBe(ConstraintSource.Building);
        resolved.Hours.Source.ShouldBe(ConstraintSource.Building);
        resolved.MaxDurationMinutes.Source.ShouldBe(ConstraintSource.Building);
    }

    [Fact]
    public void Resolve_lets_a_space_narrow_hours_alone_while_keeping_the_buildings_days()
    {
        // Columns never travel in groups: a space can narrow hours without setting days,
        // and it keeps the building's days.
        var building = CreateBuilding();
        var floor = CreateFloor(building.Id);
        var space = CreateSpace(floor.Id);
        space.SetOwnOperatingHours(new OperatingWindow(new TimeOnly(8, 0), new TimeOnly(18, 0)), building.Hours);

        var resolved = _resolver.Resolve(building, floor, space);

        resolved.Hours.Source.ShouldBe(ConstraintSource.Space);
        resolved.Hours.Value.Open.ShouldBe(new TimeOnly(8, 0));
        resolved.Days.Source.ShouldBe(ConstraintSource.Building);
    }

    [Fact]
    public void Resolve_prefers_floor_over_building_when_space_has_no_override()
    {
        var building = CreateBuilding();
        var floor = CreateFloor(building.Id);
        floor.SetOwnMaxDuration(120);
        var space = CreateSpace(floor.Id);

        var resolved = _resolver.Resolve(building, floor, space);

        resolved.MaxDurationMinutes.Source.ShouldBe(ConstraintSource.Floor);
        resolved.MaxDurationMinutes.Value.ShouldBe(120);
    }

    [Fact]
    public void EnsureHoursNarrowing_throws_when_the_child_would_widen_access()
    {
        var buildingHours = new OperatingWindow(new TimeOnly(7, 0), new TimeOnly(20, 0));
        var tooWide = new OperatingWindow(new TimeOnly(6, 0), new TimeOnly(21, 0));

        var exception = Should.Throw<BusinessException>(() => _resolver.EnsureHoursNarrowing(tooWide, buildingHours));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.HoursNotNarrower);
    }

    [Fact]
    public void EnsureDaysNarrowing_allows_a_null_override_since_null_means_inherit()
    {
        var buildingDays = OperatingDays.FromDayOfWeeks([DayOfWeek.Monday]);

        Should.NotThrow(() => _resolver.EnsureDaysNarrowing(null, buildingDays));
    }

    [Fact]
    public void FindNarrowingConflicts_flags_only_candidates_that_no_longer_fit()
    {
        var stillFits = new NarrowingCandidate(
            "Meeting Room 3C",
            OwnDays: null,
            OwnHours: new OperatingWindow(new TimeOnly(9, 0), new TimeOnly(17, 0)));

        var noLongerFits = new NarrowingCandidate(
            "Meeting Room 3B",
            OwnDays: null,
            OwnHours: new OperatingWindow(new TimeOnly(6, 0), new TimeOnly(21, 0)));

        var proposedHours = new OperatingWindow(new TimeOnly(7, 0), new TimeOnly(20, 0));

        var conflicts = _resolver.FindNarrowingConflicts(
            [stillFits, noLongerFits],
            proposedDays: null,
            proposedHours: proposedHours);

        conflicts.ShouldHaveSingleItem();
        conflicts[0].ShouldContain("Meeting Room 3B");
    }

    [Fact]
    public void FindNarrowingConflicts_ignores_candidates_that_inherit_the_field_being_tightened()
    {
        var inheritsHours = new NarrowingCandidate("Focus Pod 2-04", OwnDays: null, OwnHours: null);
        var proposedHours = new OperatingWindow(new TimeOnly(7, 0), new TimeOnly(20, 0));

        var conflicts = _resolver.FindNarrowingConflicts([inheritsHours], proposedDays: null, proposedHours: proposedHours);

        conflicts.ShouldBeEmpty();
    }
}
