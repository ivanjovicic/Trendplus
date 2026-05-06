using Api.Config;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsConnectionResolverTests
{
    private const string DefaultConnection =
        "Host=prod-default.neon.tech;Port=5432;Database=trendplus;Username=neondb_owner;Password=secret;Ssl Mode=Require;";

    private const string AnalyticsConnection =
        "Host=prod-analytics.neon.tech;Port=5432;Database=trendplus;Username=neondb_owner;Password=secret;Ssl Mode=Require;";

    private const string LoopbackAnalyticsConnection =
        "Host=127.0.0.1;Port=5434;Database=trendplus;Username=postgres;Password=postgres;";

    [Fact]
    public void ResolveDetailed_UsesExplicitAnalyticsConnection_WhenValid()
    {
        var result = AnalyticsConnectionResolver.ResolveDetailed(
            DefaultConnection,
            AnalyticsConnection,
            isDevelopment: false);

        Assert.Equal(AnalyticsConnection, result.ConnectionString);
        Assert.Equal(AnalyticsConnectionResolver.SourceAnalyticsConnection, result.Source);
        Assert.False(result.UsedFallback);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void ResolveDetailed_FallsBackToDefault_WhenAnalyticsConnectionMissingInProduction()
    {
        var warnings = new List<string>();

        var result = AnalyticsConnectionResolver.ResolveDetailed(
            DefaultConnection,
            analyticsConnection: null,
            isDevelopment: false,
            onWarning: warnings.Add);

        Assert.Equal(DefaultConnection, result.ConnectionString);
        Assert.Equal(AnalyticsConnectionResolver.SourceMissingAnalyticsFallback, result.Source);
        Assert.True(result.UsedFallback);
        Assert.NotNull(result.Warning);
        Assert.Single(warnings);
    }

    [Fact]
    public void ResolveDetailed_FallsBackToDefault_WhenAnalyticsConnectionIsLoopbackInProduction()
    {
        var warnings = new List<string>();

        var result = AnalyticsConnectionResolver.ResolveDetailed(
            DefaultConnection,
            LoopbackAnalyticsConnection,
            isDevelopment: false,
            onWarning: warnings.Add);

        Assert.Equal(DefaultConnection, result.ConnectionString);
        Assert.Equal(AnalyticsConnectionResolver.SourceLoopbackAnalyticsFallback, result.Source);
        Assert.True(result.UsedFallback);
        Assert.NotNull(result.Warning);
        Assert.Single(warnings);
    }

    [Fact]
    public void ResolveDetailed_AllowsLoopbackAnalyticsConnection_InDevelopment()
    {
        var result = AnalyticsConnectionResolver.ResolveDetailed(
            DefaultConnection,
            LoopbackAnalyticsConnection,
            isDevelopment: true);

        Assert.Equal(LoopbackAnalyticsConnection, result.ConnectionString);
        Assert.Equal(AnalyticsConnectionResolver.SourceAnalyticsConnection, result.Source);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public void ResolveDetailed_AllowsLoopbackAnalyticsConnection_InProductionWhenExplicitlyAllowed()
    {
        var result = AnalyticsConnectionResolver.ResolveDetailed(
            DefaultConnection,
            LoopbackAnalyticsConnection,
            isDevelopment: false,
            allowLoopbackInProduction: true);

        Assert.Equal(LoopbackAnalyticsConnection, result.ConnectionString);
        Assert.Equal(AnalyticsConnectionResolver.SourceAnalyticsConnection, result.Source);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public void ResolveDetailed_Throws_WhenBothConnectionsAreMissing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AnalyticsConnectionResolver.ResolveDetailed(
                defaultConnection: null,
                analyticsConnection: null,
                isDevelopment: false));
    }

    [Fact]
    public void ResolveDetailed_ReadsAllowLoopbackFlag_FromConfiguration()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = DefaultConnection,
            ["ConnectionStrings:AnalyticsConnection"] = LoopbackAnalyticsConnection,
            ["Analytics:AllowLoopbackInProduction"] = "true",
            ["ASPNETCORE_ENVIRONMENT"] = "Production"
        });

        var result = AnalyticsConnectionResolver.ResolveDetailed(configuration);

        Assert.Equal(LoopbackAnalyticsConnection, result.ConnectionString);
        Assert.Equal(AnalyticsConnectionResolver.SourceAnalyticsConnection, result.Source);
        Assert.False(result.UsedFallback);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
