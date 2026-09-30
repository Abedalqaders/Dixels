using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Dixels.Web.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Dixels.Hosting;

public class RateLimitingTests : DixelsWebTestBase
{
    private const int Limit = 2;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Registered after the module's Configure, so these small limits win.
        services.Configure<DixelsRateLimitOptions>(options =>
        {
            options.Api = new DixelsRateLimitWindow { PermitLimit = Limit, WindowSeconds = 60 };
            options.Account = new DixelsRateLimitWindow { PermitLimit = Limit, WindowSeconds = 60 };
        });
    }

    [Fact]
    public async Task Account_posts_over_the_limit_get_429_with_retry_after_and_an_abp_error()
    {
        for (var i = 0; i < Limit; i++)
        {
            (await PostLoginAsync()).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }

        var rejected = await PostLoginAsync();

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        var body = await rejected.Content.ReadAsStringAsync();
        body.ShouldContain(DixelsRateLimitingServiceCollectionExtensions.TooManyRequestsCode);
        body.ShouldContain("Wait a moment");
    }

    [Fact]
    public async Task Api_calls_over_the_limit_get_429()
    {
        for (var i = 0; i < Limit; i++)
        {
            (await Client.GetAsync("/api/abp/application-configuration")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await Client.GetAsync("/api/abp/application-configuration")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Health_checks_and_pages_are_never_throttled()
    {
        for (var i = 0; i < Limit + 3; i++)
        {
            (await Client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await Client.GetAsync("/Account/Login")).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }

    private Task<HttpResponseMessage> PostLoginAsync()
    {
        // No antiforgery token, so the page itself would refuse it — the limiter counts it first.
        return Client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["LoginInput.UserNameOrEmailAddress"] = "someone@example.com",
            ["LoginInput.Password"] = "wrong-password",
        }));
    }
}
