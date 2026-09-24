using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.SpaceManagement;

/// <summary>
/// A dated closure (or special opening) scoped to a Building, Floor, or Space — not a
/// boolean flag, since a flag can't answer "can I book this next month?" during a temporary
/// outage. Managed as delete+recreate, not in-place edit.
///
/// Closures behave differently from constraints: constraints override (the most specific
/// level wins), closures union (any level can block, and they add up). <see cref="Effect"/>
/// being <see cref="OverrideEffect.Closed"/> always wins on overlap regardless of level — a
/// space cannot re-open itself while its floor or building is closed. An
/// <see cref="OverrideEffect.Open"/> effect extends bookability beyond the resolved
/// operating days/hours (e.g. a special Saturday opening at Building level) but is beaten
/// by any overlapping <see cref="OverrideEffect.Closed"/> at any level.
/// </summary>
public class AvailabilityOverride : AuditedAggregateRoot<Guid>
{
    public const int MaxReasonDetailLength = 512;

    public OverrideScope Scope { get; private set; }
    public Guid ScopeId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public OverrideEffect Effect { get; private set; }
    public ReasonCategory ReasonCategory { get; private set; }
    public string? ReasonDetail { get; private set; }

    private AvailabilityOverride()
    {
        // EF Core
    }

    public AvailabilityOverride(
        Guid id,
        OverrideScope scope,
        Guid scopeId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        OverrideEffect effect,
        ReasonCategory reasonCategory,
        string? reasonDetail = null)
        : base(id)
    {
        Scope = scope;
        ScopeId = scopeId;
        SetPeriod(startsAt, endsAt);
        Effect = effect;
        ReasonCategory = reasonCategory;
        SetReasonDetail(reasonDetail);
    }

    private void SetPeriod(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (endsAt <= startsAt)
        {
            throw new BusinessException(DixelsDomainErrorCodes.OverrideEndsAtMustBeAfterStartsAt);
        }

        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public void SetReasonDetail(string? reasonDetail)
    {
        if (reasonDetail is not null && reasonDetail.Length > MaxReasonDetailLength)
        {
            throw new BusinessException(DixelsDomainErrorCodes.ReasonDetailTooLong);
        }

        ReasonDetail = reasonDetail;
    }
}
