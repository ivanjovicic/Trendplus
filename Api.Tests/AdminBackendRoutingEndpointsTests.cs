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
    }

    [Fact]
    public async Task UpdatePreference_RejectsSamePrimaryAndFallback()
    {
        await using var host = await BackendRoutingTestHost.CreateAsync();

        var response = await host.Client.PostAsJsonAsync("/api/admin/backend-routing", new
        {
            primaryProvider = "render",
            fallbackEnabled = true,
            fallbackProvider = "render"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePreference_AcceptsFallbackToggleOff()
    {
        await using var host = await BackendRoutingTestHost.CreateAsync();

        var response = await host.Client.PostAsJsonAsync("/api/admin/backend-routing", new
        {
            primaryProvider = "render",
            fallbackEnabled = false,
            fallbackProvider = "fly"
        });
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

        public static async Task<BackendRoutingTestHost> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddHttpClient("default", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackendRouting:Providers:RenderUrl"] = "https://example-render.local",
                ["BackendRouting:Providers:FlyUrl"] = "https://example-fly.local"
            });

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
