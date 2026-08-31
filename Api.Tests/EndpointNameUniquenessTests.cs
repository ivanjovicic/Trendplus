using Api.Endpoints;
using Api.Services.DataSources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class EndpointNameUniquenessTests
{
    [Fact]
    public async Task MappingPreviewEndpointNames_AreUniqueAcrossPublicAndAdminRoutes()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddScoped<IDataSourceMappingPreviewService, DataSourceMappingPreviewService>();
        builder.Services.AddScoped<SourceCheckpointSyncService>();

        await using var app = builder.Build();
        app.MapAdminDataSourceEndpoints();
        app.MapDataSourceMappingPreviewEndpoints();
        await app.StartAsync();

        var mappingPreviewEndpoints = app.Services
            .GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.EndsWith("/mapping-preview", StringComparison.OrdinalIgnoreCase) == true)
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .ToArray();

        Assert.Equal(2, mappingPreviewEndpoints.Length);
        Assert.Equal(2, mappingPreviewEndpoints.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("PreviewDataSourceMapping", mappingPreviewEndpoints);
        Assert.Contains("PreviewAdminDataSourceMapping", mappingPreviewEndpoints);
    }
}
