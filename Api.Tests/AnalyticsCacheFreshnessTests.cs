using System.Text.Json;
using Api.Services;
using Application.Analytics;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsCacheFreshnessTests
{
    [Fact]
    public void ColorSalesCacheMetadata_UsesSourceRefreshAndMarksOldDataStale()
    {
        var cacheCreatedAtUtc = DateTime.UtcNow.AddMinutes(-4);
        var dataRefreshAtUtc = DateTime.UtcNow.AddHours(-2);
        var json = "{\"meta\":{\"success\":true,\"dataQualityStatus\":\"good\"}}";
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = cacheCreatedAtUtc,
            DataRefreshAtUtc = dataRefreshAtUtc,
            Family = AnalyticsCachePolicy.ColorSalesFamily
        };

        var result = AllEndpoints.ApplyColorSalesCacheMetadata(
            json,
            metadata,
            AnalyticsCachePolicy.ColorSalesStats);

        using var document = JsonDocument.Parse(result);
        var meta = document.RootElement.GetProperty("meta");
        Assert.Equal(dataRefreshAtUtc, meta.GetProperty("lastRefreshAtUtc").GetDateTime());
        Assert.Equal(cacheCreatedAtUtc, meta.GetProperty("cacheCreatedAtUtc").GetDateTime());
        Assert.True(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal("STALE_CACHE", meta.GetProperty("warningCode").GetString());
        Assert.Equal("good", meta.GetProperty("dataQualityStatus").GetString());
    }

    [Fact]
    public void ColorSalesCacheMetadata_MissingMetadataFailsClosedAsStale()
    {
        var json = "{\"meta\":{\"success\":true}}";
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = DateTime.UnixEpoch,
            DataRefreshAtUtc = null,
            Family = AnalyticsCachePolicy.ColorSalesFamily
        };

        var result = AllEndpoints.ApplyColorSalesCacheMetadata(
            json,
            metadata,
            AnalyticsCachePolicy.ColorSalesStats);

        using var document = JsonDocument.Parse(result);
        var meta = document.RootElement.GetProperty("meta");
        Assert.True(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal("STALE_CACHE", meta.GetProperty("warningCode").GetString());
        Assert.Equal("warning", meta.GetProperty("dataQualityStatus").GetString());
    }

    [Fact]
    public void ColorSalesCacheMetadata_RebindsCachedTrustToCurrentFamilyAndQueryContext()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var family = OperationsAnalyticsIntegrityFamilies.SupplierShoeType;
        var generation = registry.GetGeneration(family);
        var fromUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var fingerprint = OperationsAnalyticsIntegrityContextPolicy.CreateFingerprint(
            family, generation, fromUtc, toUtc, "all", null);
        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified,
            "current-color-evidence",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "bounded_probe",
            "Color buckets reconciled.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false)
        {
            Family = family,
            ContextFingerprint = fingerprint,
            SourceGeneration = generation
        });
        var json = "{\"meta\":{\"success\":true,\"operationsIntegrityStatus\":\"verified\",\"operationsIntegrityEvidenceId\":\"cached-old-evidence\",\"operationsIntegrityContextFingerprint\":\"cached-old-context\"}}";
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = DateTime.UtcNow,
            DataRefreshAtUtc = DateTime.UtcNow,
            Family = AnalyticsCachePolicy.ColorSalesFamily
        };

        var matchingResult = AllEndpoints.ApplyColorSalesCacheMetadata(
            json, metadata, AnalyticsCachePolicy.ColorSalesStats, registry, fromUtc, toUtc, "all", null);
        var mismatchedResult = AllEndpoints.ApplyColorSalesCacheMetadata(
            json, metadata, AnalyticsCachePolicy.ColorSalesStats, registry, fromUtc.AddDays(1), toUtc, "all", null);

        using var matching = JsonDocument.Parse(matchingResult);
        var matchingMeta = matching.RootElement.GetProperty("meta");
        Assert.Equal("current-color-evidence", matchingMeta.GetProperty("operationsIntegrityEvidenceId").GetString());
        Assert.Equal(fingerprint, matchingMeta.GetProperty("operationsIntegrityContextFingerprint").GetString());
        Assert.True(matchingMeta.GetProperty("operationsIntegrityContextMatches").GetBoolean());

        using var mismatched = JsonDocument.Parse(mismatchedResult);
        var mismatchedMeta = mismatched.RootElement.GetProperty("meta");
        Assert.True(
            mismatchedMeta.TryGetProperty("operationsIntegrityEvidenceId", out var mismatchedEvidence)
                && (mismatchedEvidence.ValueKind == JsonValueKind.Null
                    || string.IsNullOrWhiteSpace(mismatchedEvidence.GetString())));
        Assert.False(mismatchedMeta.GetProperty("operationsIntegrityContextMatches").GetBoolean());
    }

    [Fact]
    public void ColorSalesCacheMetadata_RejectsPayloadWithoutMetaObject()
    {
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = DateTime.UtcNow,
            Family = AnalyticsCachePolicy.ColorSalesFamily
        };

        Assert.Throws<InvalidOperationException>(() => AllEndpoints.ApplyColorSalesCacheMetadata(
            "{\"colors\":[]}",
            metadata,
            AnalyticsCachePolicy.ColorSalesStats));
    }

    [Fact]
    public void ApplyStaleCacheWarning_UsesDataRefreshInsteadOfCacheCreation()
    {
        var cacheCreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var dataRefreshAtUtc = DateTime.UtcNow.AddHours(-2);
        var meta = new AnalyticsResponseMetaDto { Success = true };
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = cacheCreatedAtUtc,
            DataRefreshAtUtc = dataRefreshAtUtc
        };

        CachedAnalyticsEndpoints.ApplyStaleCacheWarning(
            meta,
            metadata,
            AnalyticsCachePolicy.DashboardBootstrap);

        Assert.Equal(dataRefreshAtUtc, meta.LastRefreshAtUtc);
        Assert.Equal(cacheCreatedAtUtc, meta.CacheCreatedAtUtc);
        Assert.True(meta.IsPartial);
        Assert.Equal("STALE_CACHE", meta.WarningCode);
    }

    [Fact]
    public void ApplyStaleCacheWarning_LeavesMissingDataRefreshUnknown()
    {
        var cacheCreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var meta = new AnalyticsResponseMetaDto { Success = true };
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = cacheCreatedAtUtc,
            DataRefreshAtUtc = null
        };

        CachedAnalyticsEndpoints.ApplyStaleCacheWarning(
            meta,
            metadata,
            AnalyticsCachePolicy.DashboardBootstrap);

        Assert.Null(meta.LastRefreshAtUtc);
        Assert.Equal(cacheCreatedAtUtc, meta.CacheCreatedAtUtc);
    }

    [Fact]
    public void LegacyCacheMetadata_DoesNotInventDataRefreshTimestamp()
    {
        var legacyJson = $"{{\"createdAtUtc\":\"{DateTime.UtcNow.AddHours(-2):O}\",\"family\":\"dashboard\",\"provider\":\"memory\"}}";
        var metadata = JsonSerializer.Deserialize<AnalyticsCacheEntryMetadata>(legacyJson);

        Assert.NotNull(metadata);
        Assert.Null(metadata!.DataRefreshAtUtc);
    }
}
