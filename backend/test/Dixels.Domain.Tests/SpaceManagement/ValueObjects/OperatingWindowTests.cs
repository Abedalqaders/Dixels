using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.SpaceManagement.Tests.TestFixtures;

namespace Dixels.SpaceManagement.Tests;

public class OperatingWindowTests
{
    public static IEnumerable<object[]> SharedCases()
    {
        var cases = SharedFixtureLoader.Load<OperatingWindowCase>("operating-window-cases.json");

        foreach (var testCase in cases)
        {
            yield return new object[] { testCase };
        }
    }

    [Theory]
    [MemberData(nameof(SharedCases))]
    public void IsSubsetOf_matches_the_shared_fixture(OperatingWindowCase testCase)
    {
        var parent = ToWindow(testCase.Parent);
        var child = ToWindow(testCase.Child);

        child.IsSubsetOf(parent).ShouldBe(testCase.Expected, testCase.Description);
    }

    [Fact]
    public void Constructor_rejects_equal_open_and_close()
    {
        Should.Throw<ArgumentException>(() => OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(7, 0)));
    }

    [Fact]
    public void FullDay_is_open_24_hours()
    {
        OperatingWindow.FullDay.IsOpen24Hours.ShouldBeTrue();
    }

    [Fact]
    public void IsSubsetOf_throws_on_null_parent()
    {
        var window = OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0));

        Should.Throw<ArgumentNullException>(() => window.IsSubsetOf(null!));
    }

    private static OperatingWindow ToWindow(OperatingWindowSpec spec)
        => spec.IsOpen24Hours
            ? OperatingWindow.FullDay
            : OperatingWindow.Create(TimeOnly.Parse(spec.Open), TimeOnly.Parse(spec.Close));
}

public sealed record OperatingWindowSpec(bool IsOpen24Hours, string Open, string Close);

public sealed record OperatingWindowCase(string Description, OperatingWindowSpec Parent, OperatingWindowSpec Child, bool Expected)
{
    public override string ToString() => Description;
}
