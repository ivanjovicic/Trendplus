using Api.Endpoints;
using Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests;

public sealed class AdminBackendRoutingEndpointsTests
{
    [Fact]
    public async Task GetPreference_DefaultsToRenderPrimary()
    {
        await using var host = await BackendRoutingTestHost.CreateAsync();
        using var response = await host.Client.GetAsync("/api/admin/backend-routing");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<BackendRoutingPayload>();
        Assert.NotNull(payload);
        Assert.Equal("render", payload!.PrimaryProvider);
        Assert.False(payload.FallbackEnabled);
    }

    [Fact]
    public async Task UpdatePreference_RejectsSamePrimaryAndFallback()
    {
        await using var host = await BackendRoutingTestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/backend-routing")
        {
            Content = JsonContent.Create(new
            {
                primaryProvider = "render",
                fallbackEnabled = true,
                fallbackProvider = "render"
            })
        };
        request.Headers.Add("X-Admin-Key", BackendRoutingTestHost.AdminApiKey);

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePreference_RejectsRequestWithoutAdminKey()
    {
        await using var host = await BackendRoutingTestHost.CreateAsync();

        var response = await host.Client.PostAsJsonAsync("/api/admin/backend-routing", new
        {
            primaryProvider = "render",
            fallbackEnabled = false,
            fallbackProvider = "fly"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePreference_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await BackendRoutingTestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/backend-routing")
        {
            Content = JsonContent.Create(new
            {
                primaryProvider = "render",
                fallbackEnabled = false,
                fallbackProvider = "fly"
            })
        };
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePreference_AcceptsFallbackToggleOff()
    {
        await using var host = await BackendRoutingTestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/backend-routing")
        {
            Content = JsonContent.Create(new
            {
                primaryProvider = "render",
                fallbackEnabled = false,
                fallbackProvider = "fly"
            })
        };
        request.Headers.Add("X-Admin-Key", BackendRoutingTestHost.AdminApiKey);

        using var response = await host.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<BackendRoutingPayload>();
        Assert.NotNull(payload);
        Assert.False(payload!.FallbackEnabled);
    }

    private sealed class BackendRoutingPayload
    {
        public string PrimaryProvider { get; set; } = string.Empty;
        public bool FallbackEnabled { get; set; }
        public string FallbackProvider { get; set; } = string.Empty;
    }

    private sealed class BackendRoutingTestHost : IAsyncDisposable
    {
        private BackendRoutingTestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public const string AdminApiKey = "test-admin-key";

        public static async Task<BackendRoutingTestHost> CreateAsync(bool withAdminKey = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddHttpClient("default", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            var config = new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = "web",
                ["BackendRouting:Providers:RenderUrl"] = "https://example-render.local",
                ["BackendRouting:Providers:FlyUrl"] = "https://example-fly.local"
            };

            if (withAdminKey)
            {
                config["Admin:ApiKey"] = AdminApiKey;
            }

            builder.Configuration.AddInMemoryCollection(config);

            builder.Services.AddSingleton<BackendRoutingPreferenceService>();

            var app = builder.Build();
            app.MapAdminBackendRoutingEndpoints();
            await app.StartAsync();

            return new BackendRoutingTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}
