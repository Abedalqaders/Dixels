using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Dixels.Hosting;

public class CorsPreflightTests : DixelsWebTestBase
{
    [Fact]
    public async Task Preflight_from_the_app_is_allowed_and_remembered_for_two_hours()
    {
        var appOrigin = GetRequiredService<IConfiguration>()["App:CorsOrigins"]!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First()
            .TrimEnd('/');

        var response = await SendPreflightAsync(appOrigin);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([appOrigin]);
        response.Headers.GetValues("Access-Control-Max-Age").ShouldBe(["7200"]);
    }

    [Fact]
    public async Task Preflight_from_an_unknown_origin_is_not_allowed()
    {
        var response = await SendPreflightAsync("https://not-dixels.example");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    // What the browser sends before a GET carrying the access token.
    private Task<HttpResponseMessage> SendPreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/account/my-profile");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return Client.SendAsync(request);
    }
}
