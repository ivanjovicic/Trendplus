using Domain.Model.Analytics;
using Trendplus2.Endpoints;
using Trendplus2.Dtos;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DecisionBoardEndpointsTests
{
    [Fact]
    public void BuildDecisionBoardResponse_PreservesMissingImpact_AndCapsInsufficientDataPriority()
    {
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var productDecisionCenter = new ProductDecisionCenterResponseDto
        {
            GeneratedAtUtc = generatedAtUtc,
            PeriodFromUtc = generatedAtUtc.AddDays(-30),
            PeriodToUtc = generatedAtUtc,
            Summary = new ProductDecisionCenterSummaryDto(),
            Rows =
            [
                new ProductDecisionCenterRowDto
                {
                    ProductId = 101,
                    RecommendationId = "reco-101",
                    SourceType = "product",
                    SourceKey = "product:101",
                    RecommendationType = "decision",
                    Sku = "SKU-101",
                    ProductName = "Test proizvod",
                    Revenue = 125_000m,
                    UnitsSold = 12,
                    VelocityUnitsPerDay = 0.4m,
                    MarginContribution = 12_500m,
                    MarginPct = 18m,
                    MarginCoveragePct = 80m,
                    CurrentStock = 4,
                    MinStock = 6,
                    StockGap = 2,
                    LostSalesEstimate = 0m,
                    SlowStockCapital = 0m,
                    SignalConfidencePct = 15m,
                    RecommendationAllowed = false,
                    DataQualityStatus = "insufficient_data",
                    ConfidenceLevel = "insufficient_data",
                    ConfidenceScore = 12,
                    ConfidencePct = 12,
                    ReliabilityPct = 18,
                    RecommendationStatus = "INSUFFICIENT_DATA",
                    RecommendationLabel = "Nedovoljno podataka",
                    RecommendationReason = "Nema dovoljno signala za procenu.",
                    ReasonCodes = ["insufficient_data"],
                    WarningCodes = ["insufficient_data"],
                    PrimaryDrivers = ["low_history"],
                    ExpectedImpactRsd = null,
                    RiskIfIgnored = "Rizik je još nepoznat.",
                    ExplainabilityText = "Signal je nepotpun i ne treba ga tretirati kao siguran.",
                    InputFreshnessStatus = "unknown",
                    RecommendedAction = "Sačekaj dodatne podatke."
                }
            ],
            Meta = new AnalyticsResponseMetaDto
            {
                Success = true,
                DataQualityStatus = "insufficient_data",
                GeneratedAtUtc = generatedAtUtc
            }
        };

        var response = DecisionBoardEndpoints.BuildDecisionBoardResponse(
            generatedAtUtc,
            productDecisionCenter.PeriodFromUtc,
            productDecisionCenter.PeriodToUtc,
            lastRefreshAtUtc: generatedAtUtc,
            productDecisionCenter,
            inventoryInsights: null,
            inventoryWorkflow: null,
            supplierSummary: null,
            actions: [],
            outcomeSummary: null,
            refreshStatus: null,
            dataQualityHealth: null,
            loadWarnings: [],
            dataScope: "all",
            storeId: null,
            supplierId: null);

        Assert.Equal(7, response.Sections.Count);

        var urgentSection = Assert.Single(response.Sections.Where(section => section.Key == "urgent"));
        var urgentCard = Assert.Single(urgentSection.Cards);
        Assert.Equal("insufficient_data", urgentCard.ConfidenceLevel);
        Assert.Null(urgentCard.ExpectedImpactRsd);
        Assert.True(urgentCard.PriorityScore <= 40m);

        var impactSection = Assert.Single(response.Sections.Where(section => section.Key == "impact"));
        Assert.Empty(impactSection.Cards);

        Assert.Equal("insufficient_data", response.Meta?.DataQualityStatus);
        Assert.True(response.Meta?.Success ?? false);
    }
}
