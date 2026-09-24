using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dixels.SpaceManagement;

/* Seeds the 3 default space types from the original mock design. Runs
 * automatically alongside every other IDataSeedContributor whenever
 * Dixels.DbMigrator seeds the database. */
public class SpaceTypeDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;
    private readonly IGuidGenerator _guidGenerator;

    public SpaceTypeDataSeedContributor(IRepository<SpaceType, Guid> spaceTypeRepository, IGuidGenerator guidGenerator)
    {
        _spaceTypeRepository = spaceTypeRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await CreateIfNotExistsAsync("Meeting room", IconKey.MeetingRoom);
        await CreateIfNotExistsAsync("Focus pod", IconKey.FocusPod);
        await CreateIfNotExistsAsync("Desk", IconKey.Desk);
    }

    private async Task CreateIfNotExistsAsync(string name, IconKey iconKey)
    {
        if (await _spaceTypeRepository.AnyAsync(t => t.Name == name))
        {
            return;
        }

        // autoSave: true — flushed immediately rather than left pending on the ambient unit
        // of work. SpaceManagementHierarchyDataSeedContributor seeds the same 3 default names
        // (looked up defensively, since contributor execution order isn't guaranteed) — an
        // unflushed insert here is invisible to that other contributor's own existence check
        // (a real SQL query, not the change tracker), so whichever of the two runs second ends
        // up trying to insert the same name again, a real UNIQUE constraint violation.
        await _spaceTypeRepository.InsertAsync(new SpaceType(_guidGenerator.Create(), name, iconKey), autoSave: true);
    }
}
