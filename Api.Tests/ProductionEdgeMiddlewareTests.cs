using System.Net;
using System.Text.Json;
using Api.Config;
using Api.Services.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

public sealed class ProductionEdgePolicyTests
{
    [Fact]
    public void ShouldUseHsts_IsFalseInDevelopment()
    {
        Assert.False(ProductionEdgePolicy.ShouldUseHsts(new FakeEnvironment(Environments.Development)));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ShouldUseHsts_IsTrueOutsideDevelopment(string environmentName)
    {
        Assert.True(ProductionEdgePolicy.ShouldUseHsts(new FakeEnvironment(environmentName)));
    }

    [Fact]
    public void Swagger_DefaultsToDevelopmentOnly()
    {
        var empty = new ConfigurationBuilder().Build();
        Assert.True(SwaggerExposurePolicy.IsEnabled(empty, new FakeEnvironment(Environments.Development)));
        Assert.False(SwaggerExposurePolicy.IsEnabled(empty, new FakeEnvironment(Environments.Production)));
    }

    [Fact]
    public void Swagger_RespectsExplicitOverride()
    {
        var enabledInProd = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" })
            .Build();
        var disabledInDev = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Swagger:Enabled"] = "false" })
            .Build();

        Assert.True(SwaggerExposurePolicy.IsEnabled(enabledInProd, new FakeEnvironment(Environments.Production)));
        Assert.False(SwaggerExposurePolicy.IsEnabled(disabledInDev, new FakeEnvironment(Environments.Development)));
    }

    [Fact]
    public void Cors_UsesConfiguredOrigins()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://trendplus.vercel.app",
                ["Cors:AllowedOrigins:1"] = "https://example.test"
            })
            .Build();

        var origins = CorsOriginsResolver.Resolve(config, new FakeEnvironment(Environments.Production));

        Assert.Equal(new[] { "https://trendplus.vercel.app", "https://example.test" }, origins);
    }

    [Fact]
    public void Cors_UsesDevelopmentDefaultsWhenUnconfigured()
    {
        var config = new ConfigurationBuilder().Build();
        var origins = CorsOriginsResolver.Resolve(config, new FakeEnvironment(Environments.Development));
        Assert.Contains("http://localhost:5173", origins);
    }

    [Fact]
    public void Cors_RequiresOriginsOutsideDevelopment()
    {
        var config = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() =>
            CorsOriginsResolver.Resolve(config, new FakeEnvironment(Environments.Production)));
    }

    [Fact]
    public void DependencyHealth_PublicCodesAreStableAndSafe()
    {
        Assert.Equal("missing_connection_string", DependencyHealthPublicErrors.ForMissingConnectionString());
        Assert.Equal("timeout", DependencyHealthPublicErrors.ForCanceled(requestAborted: false));
        Assert.Equal("request_aborted", DependencyHealthPublicErrors.ForCanceled(requestAborted: true));
        Assert.Equal("unavailable", DependencyHealthPublicErrors.ForUnexpectedFailure());
        Assert.True(DependencyHealthPublicErrors.IsPublicSafeCode("unavailable"));
        Assert.False(DependencyHealthPublicErrors.IsPublicSafeCode("Host 127.0.0.1 refused connection"));
    }
}

[Trait("Category", "Integration")]
public sealed class ProductionEdgeMiddlewareTests
{
    [Fact]
    public async Task Production_HttpsAfterProxyHeaders_EmitsHstsHeader()
    {
        await using var host = await EdgeTestHost.CreateAsync(
            Environments.Production,
            corsOrigins: new[] { "https://trendplus.vercel.app" },
            swaggerEnabled: false);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = "trendplus-api.onrender.com";
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Strict-Transport-Security"),
            "Expected HSTS after proxy TLS termination (X-Forwarded-Proto=https).");
        using var schemeProbe = await host.Client.SendAsync(CloneWithProto(HttpMethod.Get, "/diag/scheme", "https"));
        schemeProbe.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await schemeProbe.Content.ReadAsStreamAsync());
        Assert.Equal("https", doc.RootElement.GetProperty("scheme").GetString());
        Assert.True(doc.RootElement.GetProperty("isHttps").GetBoolean());
    }

    private static HttpRequestMessage CloneWithProto(HttpMethod method, string path, string proto)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Forwarded-Proto", proto);
        return request;
    }

    [Fact]
    public async Task Development_DoesNotEmitHstsHeader()
    {
        await using var host = await EdgeTestHost.CreateAsync(
            Environments.Development,
            corsOrigins: null,
            swaggerEnabled: true);

        using var response = await host.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Production_ForwardedHttp_DoesNotLoopAndSkipsHsts()
    {
        await using var host = await EdgeTestHost.CreateAsync(
            Environments.Production,
            corsOrigins: new[] { "https://trendplus.vercel.app" },
            swaggerEnabled: false);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "http");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.MovedPermanently, response.StatusCode);
    }

    [Fact]
    public async Task HealthDependencies_ReturnsSafeErrorCodesOnly()
    {
        await using var host = await EdgeTestHost.CreateAsync(
            Environments.Production,
            corsOrigins: new[] { "https://trendplus.vercel.app" },
            swaggerEnabled: false);

        using var response = await host.Client.GetAsync("/health/dependencies");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;
        Assert.Equal("unhealthy", root.GetProperty("status").GetString());

        var defaultError = root.GetProperty("checks").GetProperty("defaultDb").GetProperty("error").GetString();
        var analyticsError = root.GetProperty("checks").GetProperty("analyticsDb").GetProperty("error").GetString();
        Assert.True(DependencyHealthPublicErrors.IsPublicSafeCode(defaultError));
        Assert.True(DependencyHealthPublicErrors.IsPublicSafeCode(analyticsError));
        Assert.DoesNotContain("Exception", defaultError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", defaultError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOrigin_AndRejectsUnknown()
    {
        await using var host = await EdgeTestHost.CreateAsync(
            Environments.Production,
            corsOrigins: new[] { "https://trendplus.vercel.app" },
            swaggerEnabled: false);

        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/health");
        allowed.Headers.Add("Origin", "https://trendplus.vercel.app");
        using var allowedResponse = await host.Client.SendAsync(allowed);
        Assert.Equal("https://trendplus.vercel.app", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var denied = new HttpRequestMessage(HttpMethod.Get, "/health");
        denied.Headers.Add("Origin", "https://evil.example");
        using var deniedResponse = await host.Client.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Swagger_DisabledInProductionByDefault()
    {
        await using var host = await EdgeTestHost.CreateAsync(
            Environments.Production,
            corsOrigins: new[] { "https://trendplus.vercel.app" },
            swaggerEnabled: false);

        using var response = await host.Client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class EdgeTestHost : IAsyncDisposable
    {
        private EdgeTestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<EdgeTestHost> CreateAsync(
            string environmentName,
            string[]? corsOrigins,
            bool swaggerEnabled)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environmentName
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddHsts(options =>
            {
                // Default HSTS excludes localhost; tests use TestServer host names.
                options.ExcludedHosts.Clear();
            });
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            if (corsOrigins is { Length: > 0 })
            {
                builder.Configuration["Cors:AllowedOrigins:0"] = corsOrigins[0];
                for (var i = 1; i < corsOrigins.Length; i++)
                {
                    builder.Configuration[$"Cors:AllowedOrigins:{i}"] = corsOrigins[i];
                }
            }

            builder.Configuration["Swagger:Enabled"] = swaggerEnabled ? "true" : "false";

            var resolvedOrigins = CorsOriginsResolver.Resolve(builder.Configuration, builder.Environment);
            var allowedHealthOrigins = CorsOriginsResolver.ToSet(resolvedOrigins);
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                    policy.WithOrigins(resolvedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
            });

            if (swaggerEnabled)
            {
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();
            }

            var app = builder.Build();
            app.UseForwardedHeaders();
            // TestServer keeps HTTP scheme even with X-Forwarded-Proto; mirror production
            // proxy intent so HSTS middleware sees Request.IsHttps == true.
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) &&
                    string.Equals(proto.ToString(), "https", StringComparison.OrdinalIgnoreCase))
                {
                    context.Request.Scheme = "https";
                }

                await next(context);
            });
            if (ProductionEdgePolicy.ShouldUseHsts(app.Environment))
            {
                app.UseHsts();
            }

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/health") ||
                    context.Request.Path.StartsWithSegments("/ready"))
                {
                    HealthCorsHeaders.Apply(context, allowedHealthOrigins);
                    if (HttpMethods.IsOptions(context.Request.Method))
                    {
                        context.Response.StatusCode = StatusCodes.Status204NoContent;
                        return;
                    }
                }

                await next(context);
            });
            app.UseRouting();
            app.UseCors("AllowFrontend");

            if (SwaggerExposurePolicy.IsEnabled(builder.Configuration, app.Environment))
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.RoutePrefix = "swagger");
            }

            app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
            app.MapGet("/diag/scheme", (HttpContext context) => Results.Ok(new
            {
                scheme = context.Request.Scheme,
                isHttps = context.Request.IsHttps
            }));
            app.MapGet("/health/dependencies", (HttpContext context) =>
            {
                var payload = new
                {
                    status = "unhealthy",
                    checks = new
                    {
                        defaultDb = new
                        {
                            ok = false,
                            elapsedMs = 1,
                            error = DependencyHealthPublicErrors.ForUnexpectedFailure()
                        },
                        analyticsDb = new
                        {
                            ok = false,
                            elapsedMs = 1,
                            error = DependencyHealthPublicErrors.ForMissingConnectionString()
                        }
                    }
                };
                return Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
            });

            await app.StartAsync();
            return new EdgeTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}

internal sealed class FakeEnvironment : IHostEnvironment
{
    public FakeEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
        ApplicationName = "Api.Tests";
        ContentRootPath = AppContext.BaseDirectory;
        ContentRootFileProvider = new NullFileProvider();
    }

    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}
