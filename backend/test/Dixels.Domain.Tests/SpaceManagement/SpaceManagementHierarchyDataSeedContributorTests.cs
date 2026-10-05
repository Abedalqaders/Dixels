using System;
using System.Threading.Tasks;
using NSubstitute;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace Dixels.SpaceManagement.Tests;

public class SpaceManagementHierarchyDataSeedContributorTests
{
    private readonly IRepository<Building, Guid> _buildings = Substitute.For<IRepository<Building, Guid>>();

    private SpaceManagementHierarchyDataSeedContributor CreateContributor() => new(
        _buildings,
        Substitute.For<IRepository<Floor, Guid>>(),
        Substitute.For<IRepository<Space, Guid>>(),
        Substitute.For<IRepository<AvailabilityOverride, Guid>>(),
        // Never reached: the contributor returns before it looks up a space type.
        spaceTypeManager: null!,
        Substitute.For<IGuidGenerator>());

    [Fact]
    public async Task A_plain_migration_seeds_no_sample_buildings()
    {
        // What a production DbMigrator run passes when demo data is off.
        await CreateContributor().SeedAsync(new DataSeedContext().WithProperty("Dixels:DemoData", false));

        Assert.Empty(_buildings.ReceivedCalls());
    }

    [Fact]
    public async Task No_context_seeds_nothing_either()
    {
        await CreateContributor().SeedAsync(null!);

        Assert.Empty(_buildings.ReceivedCalls());
    }
}
