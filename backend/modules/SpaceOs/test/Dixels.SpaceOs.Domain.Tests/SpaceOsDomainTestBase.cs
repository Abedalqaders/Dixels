using Volo.Abp.Modularity;

namespace Dixels.SpaceOs;

/* Inherit from this class for your domain layer tests.
 * See SampleManager_Tests for example.
 */
public abstract class SpaceOsDomainTestBase<TStartupModule> : SpaceOsTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
