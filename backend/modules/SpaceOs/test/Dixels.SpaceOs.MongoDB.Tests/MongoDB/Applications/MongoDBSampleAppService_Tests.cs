using Dixels.SpaceOs.MongoDB;
using Dixels.SpaceOs.Samples;
using Xunit;

namespace Dixels.SpaceOs.MongoDb.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBSampleAppService_Tests : SampleAppService_Tests<SpaceOsMongoDbTestModule>
{

}
