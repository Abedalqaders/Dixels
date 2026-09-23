using Volo.Abp.Modularity;

namespace Dixels.SpaceOs;

/* Inherit from this class for your application layer tests.
 * See SampleAppService_Tests for example.
 */
public abstract class SpaceOsApplicationTestBase<TStartupModule> : SpaceOsTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
