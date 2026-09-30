using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Dixels.Hosting;

public class HealthCheckTests : DixelsWebTestBase
{
    [Fact]
    public async Task Live_answers_without_signing_in()
    {
        var body = await GetResponseAsStringAsync("/health/live");

        body.ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_checks_the_database()
    {
        var body = await GetResponseAsStringAsync("/health/ready");

        body.ShouldBe("Healthy");
    }
}
