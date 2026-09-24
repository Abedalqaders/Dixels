using System;
using Shouldly;
using Xunit;
using Volo.Abp;

namespace Dixels.SpaceManagement.Tests;

public class SpaceTests
{
    private static Space CreateSpace(int capacity = 8)
        => new(Guid.NewGuid(), Guid.NewGuid(), "Meeting Room 3B", Guid.NewGuid(), capacity);

    [Fact]
    public void Constructor_rejects_zero_or_negative_capacity()
    {
        var exception = Should.Throw<BusinessException>(() =>
            new Space(Guid.NewGuid(), Guid.NewGuid(), "Desk 1", Guid.NewGuid(), capacity: 0));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.CapacityMustBePositive);
    }

    [Fact]
    public void SetMinAttendees_rejects_a_value_above_capacity()
    {
        var space = CreateSpace(capacity: 8);

        var exception = Should.Throw<BusinessException>(() => space.SetMinAttendees(9));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.MinAttendeesExceedsCapacity);
        exception.Data["minAttendees"].ShouldBe(9);
        exception.Data["capacity"].ShouldBe(8);
    }

    [Fact]
    public void SetMinAttendees_null_means_no_minimum()
    {
        var space = CreateSpace(capacity: 8);
        space.SetMinAttendees(6);

        space.SetMinAttendees(null);

        space.MinAttendees.ShouldBeNull();
    }

    [Fact]
    public void SetCapacity_rejects_dropping_below_the_current_minimum_group_size()
    {
        var space = CreateSpace(capacity: 8);
        space.SetMinAttendees(6);

        var exception = Should.Throw<BusinessException>(() => space.SetCapacity(4));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.CapacityBelowMinAttendees);
        exception.Data["capacity"].ShouldBe(4);
        exception.Data["minAttendees"].ShouldBe(6);
    }

    [Fact]
    public void SetCapacity_allows_dropping_to_exactly_the_minimum_group_size()
    {
        var space = CreateSpace(capacity: 8);
        space.SetMinAttendees(6);

        Should.NotThrow(() => space.SetCapacity(6));
    }
}
