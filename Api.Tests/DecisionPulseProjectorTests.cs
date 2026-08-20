using Application.Analytics.DecisionPulse;
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
