using System;
using Shouldly;
using Xunit;
using Volo.Abp;

namespace Dixels.SpaceManagement.Tests;

public class AvailabilityOverrideTests
{
    [Fact]
    public void Constructor_rejects_endsAt_not_after_startsAt()
    {
        var now = DateTimeOffset.UtcNow;

        var exception = Should.Throw<BusinessException>(() => new AvailabilityOverride(
            Guid.NewGuid(),
            OverrideScope.Space,
            Guid.NewGuid(),
            startsAt: now,
            endsAt: now,
            OverrideEffect.Closed,
            ReasonCategory.Maintenance));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.OverrideEndsAtMustBeAfterStartsAt);
    }

    [Fact]
    public void Constructor_accepts_a_valid_period_and_optional_detail()
    {
        var now = DateTimeOffset.UtcNow;

        var closure = new AvailabilityOverride(
            Guid.NewGuid(),
            OverrideScope.Floor,
            Guid.NewGuid(),
            startsAt: now,
            endsAt: now.AddDays(3),
            OverrideEffect.Closed,
            ReasonCategory.Maintenance,
            "Replacing acoustic panels");

        closure.ReasonDetail.ShouldBe("Replacing acoustic panels");
        closure.Effect.ShouldBe(OverrideEffect.Closed);
    }

    [Fact]
    public void SetReasonDetail_rejects_text_over_the_length_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var closure = new AvailabilityOverride(
            Guid.NewGuid(), OverrideScope.Building, Guid.NewGuid(), now, now.AddHours(1),
            OverrideEffect.Open, ReasonCategory.Event);

        var tooLong = new string('x', AvailabilityOverrideConsts.MaxReasonDetailLength + 1);

        var exception = Should.Throw<BusinessException>(() => closure.SetReasonDetail(tooLong));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.ReasonDetailTooLong);
    }
}
