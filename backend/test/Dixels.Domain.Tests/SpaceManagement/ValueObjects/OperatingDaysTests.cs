using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.SpaceManagement.Tests.TestFixtures;

namespace Dixels.SpaceManagement.Tests;

public class OperatingDaysTests
{
    public static IEnumerable<object[]> SharedCases()
    {
        var cases = SharedFixtureLoader.Load<OperatingDaysCase>("operating-days-cases.json");

        foreach (var testCase in cases)
        {
            yield return new object[] { testCase };
        }
    }

    [Theory]
    [MemberData(nameof(SharedCases))]
    public void IsSubsetOf_matches_the_shared_fixture(OperatingDaysCase testCase)
    {
        var parent = ToDays(testCase.Parent);
        var child = ToDays(testCase.Child);

        child.IsSubsetOf(parent).ShouldBe(testCase.Expected, testCase.Description);
    }

    [Fact]
    public void Bit_zero_is_sunday_matching_DayOfWeek()
    {
        var days = OperatingDays.FromDayOfWeeks([DayOfWeek.Sunday]);

        days.Mask.ShouldBe(1);
    }

    [Fact]
    public void Constructor_rejects_a_mask_outside_the_valid_range()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OperatingDays(OperatingDays.AllDaysMask + 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new OperatingDays(-1));
    }

    [Fact]
    public void Everyday_contains_every_day()
    {
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            OperatingDays.Everyday.Contains(day).ShouldBeTrue();
        }
    }

    private static OperatingDays ToDays(string[] dayNames)
        => OperatingDays.FromDayOfWeeks(dayNames.Select(name => Enum.Parse<DayOfWeek>(name)));
}

public sealed record OperatingDaysCase(string Description, string[] Parent, string[] Child, bool Expected)
{
    public override string ToString() => Description;
}
