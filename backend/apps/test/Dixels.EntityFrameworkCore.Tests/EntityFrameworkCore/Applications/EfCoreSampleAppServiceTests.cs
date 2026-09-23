using Dixels.Samples;
using Xunit;

namespace Dixels.EntityFrameworkCore.Applications;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<DixelsEntityFrameworkCoreTestModule>
{

}
