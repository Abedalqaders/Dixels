using Xunit;

namespace Dixels.EntityFrameworkCore;

[CollectionDefinition(DixelsTestConsts.CollectionDefinitionName)]
public class DixelsEntityFrameworkCoreCollection : ICollectionFixture<DixelsEntityFrameworkCoreFixture>
{

}
