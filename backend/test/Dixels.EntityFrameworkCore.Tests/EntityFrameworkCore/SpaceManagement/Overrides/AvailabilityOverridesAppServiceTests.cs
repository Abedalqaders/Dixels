using System;
using System.Linq;
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

        var list = await _overridesAppService.GetListAsync(
            new GetAvailabilityOverridesInput { Scope = OverrideScope.Building, ScopeId = buildingId, IncludePast = true });

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

        var list = await _overridesAppService.GetListAsync(new GetAvailabilityOverridesInput { Scope = OverrideScope.Floor, ScopeId = floorId });

        list.Items.Count.ShouldBe(1);
        list.Items[0].Scope.ShouldBe(OverrideScope.Floor);
        list.Items[0].ScopeId.ShouldBe(floorId);
    }

    // ---- Paging, and what "closed now" reads ----

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    // Straight into the repository: the app service would announce each one.
    private Task<AvailabilityOverride> InsertAsync(Guid spaceId, DateTimeOffset starts, DateTimeOffset ends, OverrideEffect effect = OverrideEffect.Closed) =>
        WithUnitOfWorkAsync(() => _overrideRepository.InsertAsync(new AvailabilityOverride(
            Guid.NewGuid(), OverrideScope.Space, spaceId, starts, ends, effect, ReasonCategory.Maintenance, null)));

    [Fact]
    public async Task GetList_Shows_Upcoming_And_Current_Soonest_First_And_Hides_The_Past()
    {
        var spaceId = Guid.NewGuid();
        var past = await InsertAsync(spaceId, Now.AddDays(-3), Now.AddDays(-2));
        var current = await InsertAsync(spaceId, Now.AddHours(-1), Now.AddHours(1));
        var upcoming = await InsertAsync(spaceId, Now.AddDays(2), Now.AddDays(3));

        var list = await _overridesAppService.GetListAsync(new GetAvailabilityOverridesInput { Scope = OverrideScope.Space, ScopeId = spaceId });

        list.TotalCount.ShouldBe(2);
        list.Items.Select(o => o.Id).ShouldBe(new[] { current.Id, upcoming.Id });

        var withPast = await _overridesAppService.GetListAsync(
            new GetAvailabilityOverridesInput { Scope = OverrideScope.Space, ScopeId = spaceId, IncludePast = true });

        withPast.TotalCount.ShouldBe(3);
        withPast.Items.Select(o => o.Id).ShouldBe(new[] { upcoming.Id, current.Id, past.Id }); // most recent first
    }

    [Fact]
    public async Task GetList_Pages_With_The_Full_Count()
    {
        var spaceId = Guid.NewGuid();
        for (var day = 1; day <= 12; day++)
        {
            await InsertAsync(spaceId, Now.AddDays(day), Now.AddDays(day).AddHours(2));
        }

        var input = new GetAvailabilityOverridesInput { Scope = OverrideScope.Space, ScopeId = spaceId, MaxResultCount = 5, SkipCount = 10 };
        var lastPage = await _overridesAppService.GetListAsync(input);

        lastPage.TotalCount.ShouldBe(12);
        lastPage.Items.Count.ShouldBe(2);
        lastPage.Items[0].StartsAt.ShouldBe(Now.AddDays(11), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetActive_Finds_What_Is_On_Now_However_Many_Come_After_It()
    {
        var spaceId = Guid.NewGuid();
        // The bug it guards: a page of upcoming closures pushing the current one out of sight,
        // and "closed now" reading as open.
        for (var day = 1; day <= 60; day++)
        {
            await InsertAsync(spaceId, Now.AddDays(-day - 1), Now.AddDays(-day)); // past
            await InsertAsync(spaceId, Now.AddDays(day), Now.AddDays(day).AddHours(2)); // upcoming
        }

        var closedNow = await InsertAsync(spaceId, Now.AddDays(-90), Now.AddHours(1)); // started long ago, still on
        var openNow = await InsertAsync(spaceId, Now.AddMinutes(-5), Now.AddMinutes(30), OverrideEffect.Open);

        var active = await _overridesAppService.GetActiveAsync(OverrideScope.Space, spaceId);

        active.Items.Select(o => o.Id).ShouldBe(new[] { closedNow.Id, openNow.Id });
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
