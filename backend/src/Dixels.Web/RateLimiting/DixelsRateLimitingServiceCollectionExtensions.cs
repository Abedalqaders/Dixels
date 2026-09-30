using System;
using System.Globalization;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using Dixels.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Http;
using Volo.Abp.Json;

namespace Dixels.Web.RateLimiting;

/// <summary>
/// One global limiter (ASP.NET Core's built-in rate limiting) that picks a bucket per request:
/// Account form posts per IP, API calls per user (or per IP when anonymous), everything else
/// (pages, static files, health checks) unlimited. A rejected request gets 429 with Retry-After
/// and ABP's error body, so the frontend shows the message like any other API error.
/// </summary>
public static class DixelsRateLimitingServiceCollectionExtensions
{
    public const string TooManyRequestsCode = "Dixels:TooManyRequests";

    public static IServiceCollection AddDixelsRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });

        // Built from DixelsRateLimitOptions once the container exists, so a test (or another
        // module) can Configure<DixelsRateLimitOptions> and have it take effect.
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<DixelsRateLimitOptions>>((limiter, dixelsOptions) =>
            {
                var options = dixelsOptions.Value;
                Validate(options);

                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.OnRejected = WriteRejectionAsync;
                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    httpContext => SelectPartition(httpContext, options));
            });

        return services;
    }

    private static RateLimitPartition<string> SelectPartition(HttpContext httpContext, DixelsRateLimitOptions options)
    {
        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter("disabled");
        }

        var request = httpContext.Request;

        if (HttpMethods.IsPost(request.Method) && request.Path.StartsWithSegments("/Account"))
        {
            return FixedWindow($"account:{ClientIp(httpContext)}", options.Account);
        }

        if (request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/connect"))
        {
            // Runs after authentication, so a bearer token has already become httpContext.User.
            var userId = httpContext.User.FindUserId();
            return FixedWindow(
                userId.HasValue ? $"user:{userId.Value}" : $"ip:{ClientIp(httpContext)}",
                options.Api);
        }

        return RateLimitPartition.GetNoLimiter("unlimited");
    }

    private static RateLimitPartition<string> FixedWindow(string key, DixelsRateLimitWindow window)
    {
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = window.PermitLimit,
            Window = TimeSpan.FromSeconds(window.WindowSeconds),
            QueueLimit = 0,
        });
    }

    // Behind a proxy this is the real client only when forwarded headers are enabled
    // (ForwardedHeaders:Enabled) — otherwise every request would share the proxy's address.
    private static string ClientIp(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer<DixelsResource>>();
        var jsonSerializer = httpContext.RequestServices.GetRequiredService<IJsonSerializer>();

        var body = new RemoteServiceErrorResponse(
            new RemoteServiceErrorInfo(localizer[TooManyRequestsCode], code: TooManyRequestsCode));

        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync(jsonSerializer.Serialize(body), cancellationToken);
    }

    private static void Validate(DixelsRateLimitOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        foreach (var (name, window) in new[] { ("Api", options.Api), ("Account", options.Account) })
        {
            if (window.PermitLimit <= 0 || window.WindowSeconds <= 0)
            {
                throw new AbpInitializationException(
                    $"{DixelsRateLimitOptions.SectionName}:{name} needs a PermitLimit and WindowSeconds greater than zero.");
            }
        }
    }
}
