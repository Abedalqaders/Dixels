using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace Dixels.Postgres.SpaceManagement;

/// <summary>
/// What only a real Postgres can prove about a retried "add": copies of one request sent at the
/// same moment (each its own transaction, like HTTP requests) wait for each other on the key's
/// lock, so all of them answer with the one floor that was made — no duplicate, and no crash
/// on the unique index.
/// </summary>
[Collection(PostgresCollection.Name)]
public class HierarchyCreateReplayPostgresTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    [PostgresFact]
    public async Task Copies_of_one_add_sent_at_once_make_one_floor()
    {
        var floors = GetRequiredService<IFloorsAppService>();
        var buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        var floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        var building = await WithUnitOfWorkAsync(() => buildingRepository.InsertAsync(new Building(
            Guid.NewGuid(), "en", "HQ", null, "UTC", OperatingDays.Everyday, OperatingWindow.FullDay,
            maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0)));
        var request = new CreateFloorDto
        {
            BuildingId = building.Id,
            Names = [new() { Language = "en", Name = "Level 1" }],
            IdempotencyKey = Guid.NewGuid().ToString(),
        };

        var results = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => Task.Run(() => WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true), () => floors.CreateAsync(request)))));

        results.Select(r => r.Id).Distinct().ShouldHaveSingleItem();
        (await WithUnitOfWorkAsync(() => floorRepository.GetListAsync(f => f.BuildingId == building.Id))).ShouldHaveSingleItem();
    }
}
