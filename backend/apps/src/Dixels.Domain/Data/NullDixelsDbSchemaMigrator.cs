using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dixels.Data;

/* This is used if database provider does't define
 * IDixelsDbSchemaMigrator implementation.
 */
public class NullDixelsDbSchemaMigrator : IDixelsDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
