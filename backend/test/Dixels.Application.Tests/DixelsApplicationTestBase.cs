using Volo.Abp.Modularity;

namespace Dixels;

public abstract class DixelsApplicationTestBase<TStartupModule> : DixelsTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
