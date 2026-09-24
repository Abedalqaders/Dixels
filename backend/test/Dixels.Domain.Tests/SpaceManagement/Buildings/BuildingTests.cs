using System;
using Shouldly;
using Xunit;
using Volo.Abp;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement.Tests;

public class BuildingTests
{
    private static Building CreateValidBuilding()
        => new(
            Guid.NewGuid(),
            "Ridge House",
            "RH-01",
            "Asia/Amman",
            OperatingDays.Everyday,
            OperatingWindow.FullDay,
            maxDurationMinutes: 480,
            maxHorizonDays: 14,
            minLeadMinutes: 15);

    [Fact]
    public void Constructor_accepts_valid_values()
    {
        var building = CreateValidBuilding();

        building.Timezone.ShouldBe("Asia/Amman");
        building.MaxHorizonDays.ShouldBe(14);
    }

    [Fact]
    public void SetTimezone_rejects_an_unknown_timezone_id()
    {
        var building = CreateValidBuilding();

        var exception = Should.Throw<BusinessException>(() => building.SetTimezone("Not/AZone"));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.InvalidTimezone);
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    public void SetTimezone_accepts_real_iana_ids(string timezone)
    {
        var building = CreateValidBuilding();

        Should.NotThrow(() => building.SetTimezone(timezone));
    }

    [Fact]
    public void SetMaxHorizonDays_rejects_zero_or_negative()
    {
        var building = CreateValidBuilding();

        var exception = Should.Throw<BusinessException>(() => building.SetMaxHorizonDays(0));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.MaxHorizonDaysMustBePositive);
    }

    [Fact]
    public void SetMinLeadMinutes_rejects_negative()
    {
        var building = CreateValidBuilding();

        var exception = Should.Throw<BusinessException>(() => building.SetMinLeadMinutes(-1));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.MinLeadMinutesMustNotBeNegative);
    }

    [Fact]
    public void SetMaxDurationMinutes_rejects_zero_or_negative()
    {
        var building = CreateValidBuilding();

        var exception = Should.Throw<BusinessException>(() => building.SetMaxDurationMinutes(0));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.MaxDurationMustBePositive);
    }
}
