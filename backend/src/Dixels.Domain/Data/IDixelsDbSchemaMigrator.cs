using System.Threading.Tasks;

namespace Dixels.Data;

public interface IDixelsDbSchemaMigrator
{
    Task MigrateAsync();
}
