using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dixels.SpaceOs.MongoDB;

public static class SpaceOsMongoDbContextExtensions
{
    public static void ConfigureSpaceOs(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));
    }
}
