using Volo.Abp.Modularity;

namespace Dixels;

/* Inherit from this class for your domain layer tests. */
public abstract class DixelsDomainTestBase<TStartupModule> : DixelsTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
