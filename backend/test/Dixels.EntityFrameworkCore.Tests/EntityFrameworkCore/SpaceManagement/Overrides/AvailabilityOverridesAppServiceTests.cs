using System;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class AvailabilityOverridesAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IAvailabilityOverridesAppService _overridesAppService;
    private readonly IRepository<AvailabilityOverride, Guid> _overrideRepository;

    public AvailabilityOverridesAppServiceTests()
    {
        _overridesAppService = GetRequiredService<IAvailabilityOverridesAppService>();
        _overrideRepository = GetRequiredService<IRepository<AvailabilityOverride, Guid>>();
    }

    [Fact]
    public async Task Create_Then_GetList_Roundtrips_Reason_And_Effect()
    {
        var buildingId = Guid.NewGuid();
        var starts = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var ends = starts.AddDays(1);

        var created = await _overridesAppService.CreateAsync(new CreateAvailabilityOverrideDto
        {
            Scope = OverrideScope.Building,
            ScopeId = buildingId,
            StartsAt = starts,
            EndsAt = ends,
            Effect = OverrideEffect.Closed,
            ReasonCategory = ReasonCategory.Holiday,
            ReasonDetail = "Public holiday",
        });

        created.ReasonDetail.ShouldBe("Public holiday");

        var list = await _overridesAppService.GetListAsync(OverrideScope.Building, buildingId);

        list.Items.ShouldContain(o => o.Id == created.Id && o.Effect == OverrideEffect.Closed && o.ReasonCategory == ReasonCategory.Holiday);
    }

    [Fact]
    public async Task GetList_Is_Scoped_And_Does_Not_Leak_Other_Scopes_Or_ScopeIds()
    {
        var floorId = Guid.NewGuid();
        var otherFloorId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var starts = DateTimeOffset.UtcNow;
        var ends = starts.AddHours(2);

        await _overridesAppService.CreateAsync(new CreateAvailabilityOverrideDto
        {
            Scope = OverrideScope.Floor,
            ScopeId = floorId,
            StartsAt = starts,
            EndsAt = ends,
            Effect = OverrideEffect.Closed,
            ReasonCategory = ReasonCategory.Maintenance,
        });
        await _overridesAppService.CreateAsync(new CreateAvailabilityOverrideDto
        {
            Scope = OverrideScope.Floor,
            ScopeId = otherFloorId,
            StartsAt = starts,
            EndsAt = ends,
            Effect = OverrideEffect.Closed,
            ReasonCategory = ReasonCategory.Maintenance,
        });
        await _overridesAppService.CreateAsync(new CreateAvailabilityOverrideDto
        {
            Scope = OverrideScope.Space,
            ScopeId = floorId,
            StartsAt = starts,
            EndsAt = ends,
            Effect = OverrideEffect.Closed,
            ReasonCategory = ReasonCategory.Maintenance,
        });

        var list = await _overridesAppService.GetListAsync(OverrideScope.Floor, floorId);

        list.Items.Count.ShouldBe(1);
        list.Items[0].Scope.ShouldBe(OverrideScope.Floor);
        list.Items[0].ScopeId.ShouldBe(floorId);
    }

    [Fact]
    public async Task Create_With_EndsAt_Before_StartsAt_Throws()
    {
        var starts = DateTimeOffset.UtcNow;

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _overridesAppService.CreateAsync(new CreateAvailabilityOverrideDto
            {
                Scope = OverrideScope.Building,
                ScopeId = Guid.NewGuid(),
                StartsAt = starts,
                EndsAt = starts.AddHours(-1),
                Effect = OverrideEffect.Open,
                ReasonCategory = ReasonCategory.Event,
            }));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.OverrideEndsAtMustBeAfterStartsAt);
    }

    [Fact]
    public async Task Delete_Is_A_Real_Hard_Delete()
    {
        var starts = DateTimeOffset.UtcNow;
        var created = await _overridesAppService.CreateAsync(new CreateAvailabilityOverrideDto
        {
            Scope = OverrideScope.Space,
            ScopeId = Guid.NewGuid(),
            StartsAt = starts,
            EndsAt = starts.AddHours(1),
            Effect = OverrideEffect.Open,
            ReasonCategory = ReasonCategory.Other,
        });

        await _overridesAppService.DeleteAsync(created.Id);

        // No soft-delete filter exists for this entity at all — a plain repository fetch
        // (not even a filter-disabled one) must find nothing.
        (await _overrideRepository.FindAsync(created.Id)).ShouldBeNull();
    }
}
