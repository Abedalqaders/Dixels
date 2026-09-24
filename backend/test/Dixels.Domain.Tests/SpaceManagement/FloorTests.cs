using System;
using Shouldly;
using Xunit;
using Volo.Abp;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement.Tests;

public class FloorTests
{
    private static Floor CreateFloor()
        => new(Guid.NewGuid(), Guid.NewGuid(), "Level 3", floorNumber: 3);

    [Fact]
    public void New_floor_inherits_everything_by_default()
    {
        var floor = CreateFloor();

        floor.Days.ShouldBeNull();
        floor.Hours.ShouldBeNull();
        floor.MaxDurationMinutes.ShouldBeNull();
    }

    [Fact]
    public void SetOwnOperatingHours_accepts_a_window_that_narrows_the_parent()
    {
        var floor = CreateFloor();
        var buildingHours = new OperatingWindow(new TimeOnly(0, 0), new TimeOnly(23, 59));

        Should.NotThrow(() => floor.SetOwnOperatingHours(new OperatingWindow(new TimeOnly(7, 0), new TimeOnly(20, 0)), buildingHours));
        floor.Hours!.Open.ShouldBe(new TimeOnly(7, 0));
    }

    [Fact]
    public void SetOwnOperatingHours_rejects_a_window_wider_than_the_parent()
    {
        var floor = CreateFloor();
        var buildingHours = new OperatingWindow(new TimeOnly(7, 0), new TimeOnly(20, 0));

        var exception = Should.Throw<BusinessException>(() =>
            floor.SetOwnOperatingHours(new OperatingWindow(new TimeOnly(6, 0), new TimeOnly(21, 0)), buildingHours));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.HoursNotNarrower);
    }

    [Fact]
    public void SetOwnOperatingHours_null_clears_back_to_inherit()
    {
        var floor = CreateFloor();
        var buildingHours = OperatingWindow.FullDay;
        floor.SetOwnOperatingHours(new OperatingWindow(new TimeOnly(7, 0), new TimeOnly(20, 0)), buildingHours);

        floor.SetOwnOperatingHours(null, buildingHours);

        floor.Hours.ShouldBeNull();
    }

    [Fact]
    public void SetOwnOperatingDays_rejects_a_day_the_parent_does_not_allow()
    {
        var floor = CreateFloor();
        var buildingDays = OperatingDays.FromDayOfWeeks([DayOfWeek.Monday, DayOfWeek.Tuesday]);

        var exception = Should.Throw<BusinessException>(() =>
            floor.SetOwnOperatingDays(OperatingDays.FromDayOfWeeks([DayOfWeek.Monday, DayOfWeek.Saturday]), buildingDays));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.DaysNotNarrower);
    }

    [Fact]
    public void SetOwnMaxDuration_is_a_plain_override_with_no_narrowing_check()
    {
        var floor = CreateFloor();

        // A floor can cap duration below the building's without needing to "fit inside" anything.
        Should.NotThrow(() => floor.SetOwnMaxDuration(30));
        floor.MaxDurationMinutes.ShouldBe(30);
    }

    [Fact]
    public void SetOwnMaxDuration_rejects_zero_or_negative()
    {
        var floor = CreateFloor();

        var exception = Should.Throw<BusinessException>(() => floor.SetOwnMaxDuration(0));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.MaxDurationMustBePositive);
    }
}
