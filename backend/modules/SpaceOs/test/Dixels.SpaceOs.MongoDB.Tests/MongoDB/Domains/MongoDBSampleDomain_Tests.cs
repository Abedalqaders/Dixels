using Dixels.SpaceOs.Samples;
using Xunit;

namespace Dixels.SpaceOs.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBSampleDomain_Tests : SampleManager_Tests<SpaceOsMongoDbTestModule>
{

}
