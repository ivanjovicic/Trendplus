using Api.Config;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests;

public sealed class EmbeddingServiceRuntimePolicyTests
{
    [Fact]
    public void Resolve_AllowsMockInDevelopment()
    {
        var configuration = BuildConfiguration(
            ("EmbeddingService:UseMock", "true"),
            ("EmbeddingService:Timeout", "30"));

        var settings = EmbeddingServiceRuntimePolicy.Resolve(configuration, "Development");

        Assert.True(settings.UseMock);
        Assert.Null(settings.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.Timeout);
    }

    [Fact]
    public void Resolve_ThrowsInProduction_WhenMockIsEnabled()
    {
        var configuration = BuildConfiguration(
            ("EmbeddingService:UseMock", "true"),
            ("EmbeddingService:Timeout", "30"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EmbeddingServiceRuntimePolicy.Resolve(configuration, "Production"));

        Assert.Contains("UseMock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_AllowsPrivateNetworkUrlInProduction_WhenMockIsDisabled()
    {
        var configuration = BuildConfiguration(
            ("EmbeddingService:UseMock", "false"),
            ("EmbeddingService:BaseUrl", "http://10.0.0.5:8000"),
            ("EmbeddingService:Timeout", "30"));

        var settings = EmbeddingServiceRuntimePolicy.Resolve(configuration, "Production");

        Assert.False(settings.UseMock);
        Assert.Equal(new Uri("http://10.0.0.5:8000"), settings.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.Timeout);
    }

    [Fact]
    public void Resolve_RejectsPublicUrlInProduction_WhenMockIsDisabled()
    {
        var configuration = BuildConfiguration(
            ("EmbeddingService:UseMock", "false"),
            ("EmbeddingService:BaseUrl", "http://8.8.8.8:8000"),
            ("EmbeddingService:Timeout", "30"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EmbeddingServiceRuntimePolicy.Resolve(configuration, "Production"));

        Assert.Contains("private network", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfigurationRoot BuildConfiguration(params (string Key, string? Value)[] values)
    {
        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            dictionary[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dictionary)
            .Build();
    }
}
