using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Dixels.SpaceManagement.Tests;

public class SpaceTypeTests
{
    [Fact]
    public void Constructor_defaults_to_a_generic_icon()
    {
        var spaceType = new SpaceType(Guid.NewGuid(), "en", "Standing Desk");

        spaceType.IconKey.ShouldBe(IconKey.Generic);
        spaceType.FindName("en").ShouldBe("Standing Desk");
    }

    [Fact]
    public void SetIconKey_updates_the_icon()
    {
        var spaceType = new SpaceType(Guid.NewGuid(), "en", "Desk", IconKey.Desk);

        spaceType.SetIconKey(IconKey.Generic);

        spaceType.IconKey.ShouldBe(IconKey.Generic);
    }

    [Fact]
    public void SetName_adds_a_language_then_changes_it()
    {
        var spaceType = new SpaceType(Guid.NewGuid(), "en", "Desk");

        spaceType.SetName("ar", "مكتب");
        spaceType.SetName("ar", "  مكتب ثابت ");

        spaceType.Translations.Count.ShouldBe(2);
        spaceType.FindName("ar").ShouldBe("مكتب ثابت");
    }

    [Fact]
    public void A_name_is_trimmed_and_normalized_ignoring_case()
    {
        var spaceType = new SpaceType(Guid.NewGuid(), "en", "  Phone booth ");

        var translation = spaceType.Translations.Single();
        translation.Name.ShouldBe("Phone booth");
        translation.NormalizedName.ShouldBe(SpaceTypeTranslation.Normalize("phone BOOTH"));
    }

    [Fact]
    public void RemoveName_drops_only_that_language()
    {
        var spaceType = new SpaceType(Guid.NewGuid(), "en", "Desk");
        spaceType.SetName("ar", "مكتب");

        spaceType.RemoveName("ar");

        spaceType.FindName("ar").ShouldBeNull();
        spaceType.FindName("en").ShouldBe("Desk");
    }

    [Fact]
    public void ReleaseNames_marks_every_name_deleted()
    {
        var spaceType = new SpaceType(Guid.NewGuid(), "en", "Desk");
        spaceType.SetName("ar", "مكتب");

        spaceType.ReleaseNames();

        spaceType.Translations.ShouldAllBe(t => t.IsDeleted);
    }
}
