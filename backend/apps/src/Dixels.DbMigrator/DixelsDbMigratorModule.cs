using Dixels.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Dixels.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(DixelsEntityFrameworkCoreModule),
    typeof(DixelsApplicationContractsModule)
)]
public class DixelsDbMigratorModule : AbpModule
{
}
