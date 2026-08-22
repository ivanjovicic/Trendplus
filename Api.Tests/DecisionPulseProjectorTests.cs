using Application.Analytics.DecisionPulse;
using Api.Services.Analytics;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class DecisionPulseProjectorTests
{
    [Fact]
    public void Project_IncludesFreshActionableProductDecision_WithWhyAndDeepLink()
    {
        var projection = DecisionPulseProjector.Project(
        [
            Candidate(
                status: "REPLENISH",
                label: "Dopuni",
                why: "Zaliha pada ispod praga uz svežu evidenciju.",
                freshness: "fresh",
                allowed: true,
                dq: "good")
        ],
        sourceSucceeded: true);

        Assert.True(projection.SourceSucceeded);
        Assert.Single(projection.Items);
        Assert.Equal(0, projection.SuppressedCount);
        Assert.Equal(DecisionPulseProjector.DedicatedTenantScope, projection.Items[0].TenantScope);
        Assert.Equal("/analytics/products", projection.Items[0].DeepLink);
        Assert.Contains("Zaliha", projection.Items[0].WhySummary);
    }

    [Fact]
    public void Project_IncludesFreshInventoryDecision_WithWhyAndDeepLink()
    {
        var candidate = DecisionPulseService.MapInventoryCandidate(
            new InventoryActionSuggestionDto(
                "replenish|SKU-200|0|0",
                "replenish",
                "high",
                "Dopuni SKU-200",
                "Artikal pada ispod sigurnog cover-a.",
                "pending",
                200,
                "SKU-200",
                "Patika",
                "Magacin A",
                null,
                12,
                18000m,
                6,
                null,
                DateTime.UtcNow,
                91m,
                true,
                "good",
                ["low_cover"]),
            DateTime.UtcNow);

        var projection = DecisionPulseProjector.Project([candidate], sourceSucceeded: true);

        Assert.Single(projection.Items);
        Assert.Equal("inventory", projection.Items[0].SourceType);
        Assert.Equal(DecisionPulseProjector.InventoryDeepLink, projection.Items[0].DeepLink);
        Assert.Equal("REPLENISH", projection.Items[0].RecommendationStatus);
        Assert.Contains("cover", projection.Items[0].WhySummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_IncludesFreshSupplierDecision_WithWhyAndDeepLink()
    {
        var trust = new ScorecardTrustMetadata(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            "all",
            "all",
            "2026-08-22",
            "good",
            UsedFallback: false,
            FallbackReason: null,
            FallbackReasonCode: null,
            LastRefreshAtUtc: DateTime.UtcNow,
            RowCount: 10,
            IgnoredRowCount: 0,
            ZeroRevenueRowsExcludedCount: 0,
            MissingSupplierNameCount: 0,
            HasData: true,
            HasExplicitDateRange: true,
            RecommendationAllowed: true,
            NoSilentFallback: true,
            WindowDays: 30,
            DataScope: "all",
            Coverage: "good",
            DataNote: null);

        var candidate = DecisionPulseService.MapSupplierCandidate(
            new SummarySupplierItem(
                12,
                "Dobavljač X",
                25000m,
                0.82m,
                0.79m,
                "EXPAND",
                77m,
                88m,
                "good",
                "Signal ukazuje na širenje saradnje.",
                ["supplier_grow"]),
            trust,
            DateTime.UtcNow);

        var projection = DecisionPulseProjector.Project([candidate], sourceSucceeded: true);

        Assert.Single(projection.Items);
        Assert.Equal("supplier", projection.Items[0].SourceType);
        Assert.Equal(DecisionPulseProjector.SupplierDeepLink, projection.Items[0].DeepLink);
        Assert.Equal("BOOST", projection.Items[0].RecommendationStatus);
        Assert.Contains("širenje", projection.Items[0].WhySummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_SuppressesStaleInsufficientBlockedAndEmptyWhy()
    {
        var projection = DecisionPulseProjector.Project(
        [
            Candidate(status: "REPLENISH", why: "ok", freshness: "stale", allowed: true, dq: "good"),
            Candidate(status: "INSUFFICIENT_DATA", why: "ok", freshness: "fresh", allowed: true, dq: "good"),
            Candidate(status: "REPLENISH", why: "ok", freshness: "fresh", allowed: false, dq: "good"),
            Candidate(status: "REPLENISH", why: "   ", freshness: "fresh", allowed: true, dq: "good"),
            Candidate(status: "REPLENISH", why: "ok", freshness: "fresh", allowed: true, dq: "insufficient_data")
        ],
        sourceSucceeded: true);

        Assert.True(projection.SourceSucceeded);
        Assert.Empty(projection.Items);
        Assert.Equal(5, projection.SuppressedCount);
    }

    [Fact]
    public void Project_SourceError_DoesNotInventActionableItemsOrZeroKpis()
    {
        var projection = DecisionPulseProjector.Project(
            [Candidate(status: "REPLENISH", why: "should not appear", freshness: "fresh", allowed: true, dq: "good")],
            sourceSucceeded: false,
            failureCategory: "source_error",
            failureMessage: "PDC failed");

        Assert.False(projection.SourceSucceeded);
        Assert.Empty(projection.Items);
        Assert.Equal("source_error", projection.FailureCategory);
        Assert.Equal(0, projection.SuppressedCount);
    }

    [Fact]
    public void EmailComposer_OmitsSecretsAndRowPayloads()
    {
        var html = DecisionPulseEmailComposer.BuildHtmlBody(
        [
            new DecisionPulseItem(
                "id-1",
                "product",
                "101",
                "SKU-101 — Patika",
                "Dopuni jer je cover nizak.",
                ["LOW_COVER"],
                "REPLENISH",
                "Dopuni",
                "good",
                "fresh",
                "/analytics/products",
                DateTime.UtcNow,
                DecisionPulseProjector.DedicatedTenantScope)
        ],
        new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains("Zašto:", html);
        Assert.Contains("Izvor: product", html);
        Assert.Contains("/analytics/products", html);
        Assert.DoesNotContain("password", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kolicina", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PurchasePrice", html, StringComparison.OrdinalIgnoreCase);
    }

    private static DecisionPulseCandidate Candidate(
        string status,
        string why,
        string freshness,
        bool allowed,
        string dq,
        string label = "Akcija")
        => new(
            "rec-1",
            "product",
            "101",
            "SKU-101",
            why,
            ["REASON"],
            status,
            label,
            dq,
            freshness,
            allowed,
            DecisionPulseProjector.ProductDeepLink,
            DateTime.UtcNow);
}
