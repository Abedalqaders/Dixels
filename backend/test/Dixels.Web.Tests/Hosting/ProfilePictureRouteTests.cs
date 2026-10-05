using System.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Dixels.Hosting;

/// <summary>
/// My profile reads, uploads and removes the picture at /api/app/profile-picture. The app
/// service's own tests call it directly, so they can't tell whether the address exists; this
/// looks in the app's routing table for it, with each method My profile uses.
/// </summary>
public class ProfilePictureRouteTests : DixelsWebTestBase
{
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public void The_picture_address_takes_every_method_My_profile_uses(string method)
    {
        var endpoints = GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        endpoints.ShouldContain(e =>
            e.RoutePattern.RawText == "api/app/profile-picture"
            && e.Metadata.GetMetadata<HttpMethodMetadata>() != null
            && e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
    }
}
