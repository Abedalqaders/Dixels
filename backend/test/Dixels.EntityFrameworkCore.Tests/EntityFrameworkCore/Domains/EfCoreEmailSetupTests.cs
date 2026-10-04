using Dixels.Emailing;
using Xunit;

namespace Dixels.EntityFrameworkCore.Domains;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class EfCoreEmailSetupTests : EmailSetupTests<DixelsEntityFrameworkCoreTestModule>
{

}
