using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Dixels.Bookings;
using Dixels.EntityFrameworkCore;
using Dixels.Localization;
using Dixels.MultiTenancy;
using Dixels.Web.Components.AccountTheme;
using Dixels.Web.Menus;
using Dixels.Web.RateLimiting;
using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.OpenApi;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Ui.LayoutHooks;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity.Web;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.Web;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement.Web;
using Volo.Abp.Swashbuckle;
using Volo.Abp.OpenIddict;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.UI;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;

namespace Dixels.Web;

[DependsOn(
    typeof(DixelsHttpApiModule),
    typeof(DixelsApplicationModule),
    typeof(DixelsEntityFrameworkCoreModule),
    typeof(AbpAutofacModule),
    typeof(AbpIdentityWebModule),
    typeof(AbpSettingManagementWebModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpDistributedLockingModule),
    typeof(AbpSwashbuckleModule)
    )]
public class DixelsWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(DixelsResource),
                typeof(DixelsDomainModule).Assembly,
                typeof(DixelsDomainSharedModule).Assembly,
                typeof(DixelsApplicationModule).Assembly,
                typeof(DixelsApplicationContractsModule).Assembly,
                typeof(DixelsWebModule).Assembly
            );
        });

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("Dixels");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                // Outside Development the certificate and its password come from configuration
                // (AuthServer__CertificatePath / AuthServer__CertificatePassword env vars), never source.
                var certificatePath = configuration["AuthServer:CertificatePath"] ?? "openiddict.pfx";
                var certificatePassword = configuration["AuthServer:CertificatePassword"];
                if (string.IsNullOrWhiteSpace(certificatePassword))
                {
                    throw new AbpInitializationException("AuthServer:CertificatePassword is required outside Development.");
                }

                serverBuilder.AddProductionEncryptionAndSigningCertificate(certificatePath, certificatePassword);
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        ConfigureAuthentication(context);
        ConfigureUrls(configuration);
        ConfigureBundles();
        ConfigureVirtualFileSystem(hostingEnvironment);
        ConfigureNavigationServices();
        ConfigureSwaggerServices(context.Services);
        ConfigureCors(context.Services, configuration);
        ConfigureForwardedHeaders(context.Services, configuration);
        ConfigureDataProtection(context.Services, configuration);
        ConfigureHealthChecks(context.Services);
        ConfigureDistributedLocking(context.Services, configuration);
        ConfigureRateLimiting(context.Services, configuration);

        context.Services.AddMapperlyObjectMapper<DixelsWebModule>();
    }

    // In production the app sits behind a TLS-terminating proxy or load balancer. With this on,
    // the X-Forwarded-For/-Proto/-Host headers it adds become the request's client IP, scheme
    // and host, so OpenIddict issues https URLs and rate limits see real clients. Off by default:
    // a server reachable directly must not let callers pick their own IP through a header.
    private void ConfigureForwardedHeaders(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
        {
            return;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            // The proxy's address inside a container network isn't known up front; trust the one
            // hop in front of us (ForwardLimit stays 1, so only the value that proxy wrote counts).
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    // Sign-in cookies and antiforgery tokens are encrypted with data protection keys. Inside a
    // container those keys would vanish on every restart (signing everyone out of the login
    // pages), so production points DataProtection:KeysPath at a persistent volume.
    private void ConfigureDataProtection(IServiceCollection services, IConfiguration configuration)
    {
        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            return;
        }

        services.AddDataProtection()
            .SetApplicationName("Dixels")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }

    // /health/live: the process answers (restart it if not). /health/ready: it can also reach
    // the database (stop sending it traffic if not). Both are anonymous and unthrottled.
    private void ConfigureHealthChecks(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<DixelsDbContext>(tags: new[] { HealthCheckTags.Ready });
    }

    // With two or more API servers, background work must run on one of them at a time: ABP's job
    // worker, OpenIddict's token cleanup and the booking reminders all take an IAbpDistributedLock
    // first. Without a provider ABP falls back to an in-process lock, which every server gets.
    // Postgres advisory locks need no extra infrastructure, but hold a session-level connection
    // while held — so not through PgBouncer in transaction-pooling mode.
    private void ConfigureDistributedLocking(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDistributedLockProvider>(_ =>
            new PostgresDistributedSynchronizationProvider(configuration.GetConnectionString("Default")!));
    }

    private void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        Configure<DixelsRateLimitOptions>(configuration.GetSection(DixelsRateLimitOptions.SectionName));
        services.AddDixelsRateLimiting();
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });

        // OpenIddict rejects plain-http requests (error ID2083). Turning this off is solely for a
        // LAN test server without TLS: passwords and tokens then cross the network unencrypted.
        if (!context.Services.GetConfiguration().GetValue("AuthServer:RequireHttpsMetadata", true))
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                options.DisableTransportSecurityRequirement = true;
            });
        }
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );
        });

        // Light/dark on ABP's sign-in pages, following the web app (Components/AccountTheme).
        Configure<AbpLayoutHookOptions>(options =>
        {
            options.Add(LayoutHooks.Head.Last, typeof(AccountThemeHeadViewComponent), layout: StandardLayouts.Account);
            options.Add(LayoutHooks.Body.Last, typeof(AccountThemeToggleViewComponent), layout: StandardLayouts.Account);
        });
    }

    private void ConfigureVirtualFileSystem(IWebHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<DixelsDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Dixels.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<DixelsDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Dixels.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<DixelsApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Dixels.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<DixelsApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Dixels.Application"));
                options.FileSets.ReplaceEmbeddedByPhysical<DixelsWebModule>(hostingEnvironment.ContentRootPath);
            });
        }
    }

    private void ConfigureNavigationServices()
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new DixelsMenuContributor());
        });
    }

    // No auto API controllers: every endpoint is an explicit controller in Dixels.HttpApi
    // (Controllers/...), so routes, verbs and bindings are declared in one visible place
    // instead of being derived from app service method names.

    private void ConfigureCors(IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        (configuration["App:CorsOrigins"] ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(origin => origin.TrimEnd('/'))
                        .ToArray()
                    )
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    // The app is on another origin, so every call carrying the access token is
                    // preceded by an OPTIONS "may I?" check. Without a max age Chrome forgets the
                    // answer after 5 seconds and asks again; 2 hours is Chrome's own cap. The cost:
                    // a CORS change (a new allowed header) can take that long to reach a browser
                    // that already asked.
                    .SetPreflightMaxAge(TimeSpan.FromHours(2));
            });
        });
    }

    private void ConfigureSwaggerServices(IServiceCollection services)
    {
        services.AddAbpSwaggerGen(
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Dixels API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);

                // Nullable reference types are the source of truth for "can this be null": a
                // non-nullable string is emitted as a required, non-nullable field, not as an
                // optional "string | null" — so the frontend types generated from this document
                // (frontend/scripts/fetch-openapi.mjs) match the DTOs exactly.
                options.SupportNonNullableReferenceTypes();
                options.NonNullableReferenceTypesAsRequired();
                options.SchemaFilter<Dixels.Swagger.RequireNonNullablePropertiesSchemaFilter>();
                // OpenAPI 3.0 can't put "nullable" on a bare $ref, so a nullable DTO property
                // (a booking's RecurrenceDto?) came out as the DTO alone, never null. Wrapped in
                // allOf it can say both: the generated type reads "RecurrenceDto | null".
                options.UseAllOfToExtendReferenceSchemas();
            }
        );
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();
        var configuration = context.GetConfiguration();

        // First, so everything after it sees the real client IP and scheme.
        if (configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
        {
            app.UseForwardedHeaders();
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseCorrelationId();
        app.MapAbpStaticAssets();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        // After authentication, so API calls are counted per signed-in user.
        app.UseRateLimiter();

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        // Swagger is a development tool: it exposes every route and the OAuth client used to
        // try them. Off outside Development unless Swagger:Enabled turns it on (calling an
        // endpoint still needs a signed-in user with the right permissions).
        if (env.IsDevelopment() || configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseAbpSwaggerUI(options =>
            {
                // Relative to the UI page, so it also resolves when the app is hosted under a
                // sub-path (e.g. an IIS application at /backend).
                options.SwaggerEndpoint("v1/swagger.json", "Dixels API");
            });
        }

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
            endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains(HealthCheckTags.Ready)
            });
        });
    }

    // Here rather than in the domain module, so only the API host sends reminders and cleans up
    // guests' details — the migrator loads the domain too and must not.
    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<BookingReminderWorker>();
        await context.AddBackgroundWorkerAsync<ExternalGuestCleanupWorker>();
    }

    private static class HealthCheckTags
    {
        public const string Ready = "ready";
    }
}
