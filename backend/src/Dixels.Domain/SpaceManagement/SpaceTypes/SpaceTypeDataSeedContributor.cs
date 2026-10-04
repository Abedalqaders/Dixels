using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace Dixels.SpaceManagement;

/* Seeds the 3 default space types from the original mock design (BuiltInSpaceTypes), with
 * their English and Arabic names. Runs automatically alongside every other
 * IDataSeedContributor whenever Dixels.DbMigrator seeds the database — on an existing
 * database too, where it only adds an Arabic name a built-in type doesn't have yet. */
public class SpaceTypeDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly SpaceTypeManager _spaceTypeManager;

    public SpaceTypeDataSeedContributor(SpaceTypeManager spaceTypeManager)
    {
        _spaceTypeManager = spaceTypeManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        foreach (var builtIn in BuiltInSpaceTypes.All)
        {
            await _spaceTypeManager.EnsureBuiltInAsync(builtIn);
        }
    }
}
