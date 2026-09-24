using System;
using Shouldly;
using Xunit;

namespace Dixels.SpaceManagement.Tests;

public class SpaceTypeTests
{
    [Fact]
    public void Constructor_defaults_to_a_generic_icon()
    {
        var spaceType = new SpaceType(Guid.NewGuid(), "Standing Desk");

        spaceType.IconKey.ShouldBe(IconKey.Generic);
        spaceType.Name.ShouldBe("Standing Desk");
    }

    [Fact]
    public void SetIconKey_updates_the_icon()
    {
        var spaceType = new SpaceType(Guid.NewGuid(), "Desk", IconKey.Desk);

        spaceType.SetIconKey(IconKey.Generic);

        spaceType.IconKey.ShouldBe(IconKey.Generic);
    }
}
