using Dixels.Samples;
using Xunit;

namespace Dixels.EntityFrameworkCore.Domains;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<DixelsEntityFrameworkCoreTestModule>
{

}
