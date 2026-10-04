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
            "en",
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

    [Fact]
    public void A_new_building_warns_about_overlapping_bookings_by_default()
    {
        CreateValidBuilding().OwnOverlapPolicy.ShouldBe(OwnOverlapPolicy.Warn);
    }

    [Fact]
    public void SetOwnOverlapPolicy_rejects_an_unknown_value()
    {
        var building = CreateValidBuilding();

        var exception = Should.Throw<BusinessException>(() => building.SetOwnOverlapPolicy((OwnOverlapPolicy)42));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.InvalidOwnOverlapPolicy);
    }

    [Fact]
    public void Recurring_bookings_default_to_90_days_or_the_normal_horizon_if_longer()
    {
        CreateValidBuilding().MaxSeriesHorizonDays.ShouldBe(90);
        new Building(Guid.NewGuid(), "en", "B", null, "UTC", OperatingDays.Everyday, OperatingWindow.FullDay,
            maxDurationMinutes: 60, maxHorizonDays: 120, minLeadMinutes: 0).MaxSeriesHorizonDays.ShouldBe(120);
    }

    [Fact]
    public void The_series_horizon_cannot_be_shorter_than_the_normal_one()
    {
        var building = CreateValidBuilding(); // 14-day horizon

        var ex = Should.Throw<BusinessException>(() => building.SetMaxSeriesHorizonDays(7));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.MaxSeriesHorizonTooShort);
    }

    [Fact]
    public void Lengthening_the_normal_horizon_past_the_series_one_pulls_it_along()
    {
        var building = CreateValidBuilding();

        building.SetMaxHorizonDays(200);

        building.MaxSeriesHorizonDays.ShouldBe(200);
    }
}
