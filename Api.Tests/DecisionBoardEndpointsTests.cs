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
        var productDecisionCenter = CreateProductDecisionCenter(
            generatedAtUtc,
            CreateProductRow(
                productId: 101,
                recommendationStatus: "INSUFFICIENT_DATA",
                dataQualityStatus: "insufficient_data",
                confidenceLevel: "insufficient_data",
                confidenceScore: 12,
                lostSalesEstimate: 0m,
                expectedImpactRsd: null,
                recommendationAllowed: false,
                warningCodes: ["insufficient_data"],
                reasonCodes: ["insufficient_data"]));

        var response = BuildBoard(generatedAtUtc, productDecisionCenter);

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

    [Theory]
    [InlineData("REPLENISH", 18500.0)]
    [InlineData("BOOST", 9250.0)]
    public void BuildDecisionBoardResponse_UsesPdcExpectedImpact_ForReplenishAndBoost(
        string recommendationStatus,
        double expectedImpact)
    {
        var expectedImpactRsd = (decimal)expectedImpact;
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var productDecisionCenter = CreateProductDecisionCenter(
            generatedAtUtc,
            CreateProductRow(
                productId: 201,
                recommendationStatus: recommendationStatus,
                dataQualityStatus: "good",
                confidenceLevel: "high",
                confidenceScore: 82,
                lostSalesEstimate: expectedImpactRsd,
                expectedImpactRsd: expectedImpactRsd));

        var response = BuildBoard(generatedAtUtc, productDecisionCenter);
        var productCard = Assert.Single(FindProductCards(response));

        Assert.Equal(expectedImpactRsd, productCard.ExpectedImpactRsd);

        var impactSection = Assert.Single(response.Sections.Where(section => section.Key == "impact"));
        Assert.Contains(impactSection.Cards, card => card.Id == productCard.Id);
    }

    [Theory]
    [InlineData("REPLENISH")]
    [InlineData("BOOST")]
    [InlineData("FIX_DATA")]
    [InlineData("INSUFFICIENT_DATA")]
    [InlineData("MARKDOWN")]
    [InlineData("DO_NOT_ORDER")]
    public void BuildDecisionBoardResponse_DoesNotFallbackLostSales_WhenPdcExpectedImpactIsNull(
        string recommendationStatus)
    {
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var isBlocked = recommendationStatus is "FIX_DATA" or "INSUFFICIENT_DATA";
        var productDecisionCenter = CreateProductDecisionCenter(
            generatedAtUtc,
            CreateProductRow(
                productId: 301,
                recommendationStatus: recommendationStatus,
                dataQualityStatus: isBlocked ? "critical" : "good",
                confidenceLevel: isBlocked ? "insufficient_data" : "medium",
                confidenceScore: isBlocked ? 20 : 70,
                lostSalesEstimate: 42_000m,
                slowStockCapital: 33_000m,
                expectedImpactRsd: null,
                recommendationAllowed: !isBlocked,
                warningCodes: isBlocked ? ["critical_data"] : [],
                reasonCodes: isBlocked ? ["fix_data"] : ["ok"]));

        var response = BuildBoard(generatedAtUtc, productDecisionCenter);
        var productCard = Assert.Single(FindProductCards(response));

        Assert.Null(productCard.ExpectedImpactRsd);

        var impactSection = Assert.Single(response.Sections.Where(section => section.Key == "impact"));
        Assert.DoesNotContain(impactSection.Cards, card => card.Id == productCard.Id);
    }

    [Theory]
    [InlineData("MARKDOWN", 27500.0)]
    [InlineData("DO_NOT_ORDER", 14000.0)]
    public void BuildDecisionBoardResponse_UsesSlowStockImpactOnlyViaExpectedImpactRsd(
        string recommendationStatus,
        double slowStock)
    {
        var slowStockCapital = (decimal)slowStock;
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);

        var withPdcImpact = CreateProductDecisionCenter(
            generatedAtUtc,
            CreateProductRow(
                productId: 401,
                recommendationStatus: recommendationStatus,
                dataQualityStatus: "good",
                confidenceLevel: "medium",
                confidenceScore: 68,
                lostSalesEstimate: 99_000m,
                slowStockCapital: slowStockCapital,
                expectedImpactRsd: slowStockCapital));

        var withPdcImpactResponse = BuildBoard(generatedAtUtc, withPdcImpact);
        var withImpactCard = Assert.Single(FindProductCards(withPdcImpactResponse));
        Assert.Equal(slowStockCapital, withImpactCard.ExpectedImpactRsd);
        Assert.Contains(
            Assert.Single(withPdcImpactResponse.Sections.Where(section => section.Key == "impact")).Cards,
            card => card.Id == withImpactCard.Id);

        var withoutPdcImpact = CreateProductDecisionCenter(
            generatedAtUtc,
            CreateProductRow(
                productId: 402,
                recommendationStatus: recommendationStatus,
                dataQualityStatus: "good",
                confidenceLevel: "medium",
                confidenceScore: 68,
                lostSalesEstimate: 99_000m,
                slowStockCapital: slowStockCapital,
                expectedImpactRsd: null));

        var withoutPdcImpactResponse = BuildBoard(generatedAtUtc, withoutPdcImpact);
        var withoutImpactCard = Assert.Single(FindProductCards(withoutPdcImpactResponse));
        Assert.Null(withoutImpactCard.ExpectedImpactRsd);
        Assert.DoesNotContain(
            Assert.Single(withoutPdcImpactResponse.Sections.Where(section => section.Key == "impact")).Cards,
            card => card.Id == withoutImpactCard.Id);
    }

    [Fact]
    public void BuildDecisionBoardResponse_CapsBlockedSupplierCards_AndKeepsTrustBlocker()
    {
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var productDecisionCenter = CreateProductDecisionCenter(
            generatedAtUtc,
            CreateProductRow(
                productId: 501,
                recommendationStatus: "REPLENISH",
                dataQualityStatus: "good",
                confidenceLevel: "high",
                confidenceScore: 80,
                expectedImpactRsd: 15_000m));

        var blockedSupplier = CreateSupplierItem(
            supplierId: 77,
            supplierName: "High revenue blocked supplier",
            revenue: 900_000m,
            confidenceScore: 95m,
            recommendationCode: "EXPAND");

        var supplierSummary = CreateSupplierSummary(
            generatedAtUtc,
            recommendationAllowed: false,
            usedFallback: true,
            grow: [blockedSupplier],
            risk: []);

        var response = BuildBoard(generatedAtUtc, productDecisionCenter, supplierSummary);

        var supplierCards = response.Sections
            .SelectMany(section => section.Cards)
            .Where(card => card.Kind == "supplier")
            .DistinctBy(card => card.Id)
            .ToList();

        var blockedCard = Assert.Single(supplierCards);
        Assert.Equal("insufficient_data", blockedCard.ConfidenceLevel);
        Assert.Equal("insufficient_data", blockedCard.DataQualityStatus);
        Assert.True(blockedCard.PriorityScore <= 40m);
        Assert.Equal(0m, blockedCard.ImpactScore);
        Assert.Contains("supplier_recommendation_blocked", blockedCard.WarningCodes);
        Assert.Contains("signal_check", blockedCard.SourceKey, StringComparison.Ordinal);
        Assert.Contains("Signal check", blockedCard.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Proveri pouzdanost", blockedCard.RecommendedNextAction, StringComparison.OrdinalIgnoreCase);

        var impactSection = Assert.Single(response.Sections.Where(section => section.Key == "impact"));
        Assert.DoesNotContain(impactSection.Cards, card => card.Kind == "supplier");
        Assert.Contains(impactSection.Cards, card => card.Kind == "product");

        var urgentSection = Assert.Single(response.Sections.Where(section => section.Key == "urgent"));
        Assert.DoesNotContain(urgentSection.Cards, card => card.Kind == "supplier");

        var blockers = Assert.Single(response.Sections.Where(section => section.Key == "blockers"));
        Assert.Contains(blockers.Cards, card => card.Id == "blocker-supplier-trust");

        var supplierRisk = Assert.Single(response.Sections.Where(section => section.Key == "supplierRisk"));
        Assert.Contains(supplierRisk.Cards, card => card.Id == blockedCard.Id);
    }

    [Fact]
    public void BuildDecisionBoardResponse_AllowsHighConfidenceSupplier_WhenRecommendationAllowed()
    {
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var productDecisionCenter = CreateProductDecisionCenter(generatedAtUtc);

        var allowedSupplier = CreateSupplierItem(
            supplierId: 88,
            supplierName: "Actionable grow supplier",
            revenue: 900_000m,
            confidenceScore: 95m,
            recommendationCode: "EXPAND");

        var supplierSummary = CreateSupplierSummary(
            generatedAtUtc,
            recommendationAllowed: true,
            usedFallback: false,
            grow: [allowedSupplier],
            risk: []);

        var response = BuildBoard(generatedAtUtc, productDecisionCenter, supplierSummary);
        var supplierCard = Assert.Single(
            response.Sections
                .SelectMany(section => section.Cards)
                .Where(card => card.Kind == "supplier")
                .DistinctBy(card => card.Id));

        Assert.Equal("high", supplierCard.ConfidenceLevel);
        Assert.True(supplierCard.PriorityScore > 40m);
        Assert.Equal(900_000m, supplierCard.ImpactScore);
        Assert.DoesNotContain("supplier_recommendation_blocked", supplierCard.WarningCodes);
        Assert.Contains("negotiation", supplierCard.SourceKey, StringComparison.Ordinal);

        var impactSection = Assert.Single(response.Sections.Where(section => section.Key == "impact"));
        Assert.Contains(impactSection.Cards, card => card.Id == supplierCard.Id);

        var blockers = Assert.Single(response.Sections.Where(section => section.Key == "blockers"));
        Assert.DoesNotContain(blockers.Cards, card => card.Id == "blocker-supplier-trust");
    }

    [Fact]
    public void BuildDecisionBoardResponse_TreatsEmptyActionsAsHealthySourceState()
    {
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var productDecisionCenter = CreateProductDecisionCenter(
            generatedAtUtc,
            CreateProductRow(
                productId: 601,
                recommendationStatus: "REPLENISH",
                dataQualityStatus: "good",
                confidenceLevel: "high",
                confidenceScore: 80,
                expectedImpactRsd: 12_000m));

        var response = BuildBoard(generatedAtUtc, productDecisionCenter, loadWarnings: []);

        var actionsSource = Assert.Single(response.SourceStates, state => state.SourceKey == "analytics-actions");
        Assert.Equal("good", actionsSource.Status);
        Assert.Empty(actionsSource.WarningCodes);
        Assert.Contains("validan", actionsSource.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no_actions", response.Warnings);
        Assert.DoesNotContain("analytics_actions_unavailable", response.Warnings);
        Assert.DoesNotContain("insufficient_data", response.Warnings);

        var actionsMetric = Assert.Single(response.Metrics, metric => metric.Label == "Otvorene akcije");
        Assert.Equal("good", actionsMetric.Tone);
    }

    [Fact]
    public void BuildDecisionBoardResponse_MarksActionsInsufficient_WhenActionsServiceUnavailable()
    {
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var productDecisionCenter = CreateProductDecisionCenter(
            generatedAtUtc,
            CreateProductRow(
                productId: 602,
                recommendationStatus: "REPLENISH",
                dataQualityStatus: "good",
                confidenceLevel: "high",
                confidenceScore: 80,
                expectedImpactRsd: 12_000m));

        var response = BuildBoard(
            generatedAtUtc,
            productDecisionCenter,
            loadWarnings: ["analytics_actions_unavailable"]);

        var actionsSource = Assert.Single(response.SourceStates, state => state.SourceKey == "analytics-actions");
        Assert.Equal("insufficient_data", actionsSource.Status);
        Assert.Contains("analytics_actions_unavailable", actionsSource.WarningCodes);
        Assert.Contains("nije dostupna", actionsSource.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("analytics_actions_unavailable", response.Warnings);
        Assert.DoesNotContain("no_actions", response.Warnings);
    }

    [Theory]
    [InlineData(false, "good")]
    [InlineData(true, "insufficient_data")]
    public void ResolveAnalyticsActionsSourceState_DistinguishesEmptyFromUnavailable(
        bool unavailable,
        string expectedStatus)
    {
        var resolved = DecisionBoardEndpoints.ResolveAnalyticsActionsSourceState([], unavailable);
        Assert.Equal(expectedStatus, resolved.Status);
        if (unavailable)
        {
            Assert.Contains("analytics_actions_unavailable", resolved.WarningCodes);
        }
        else
        {
            Assert.Empty(resolved.WarningCodes);
        }
    }

    [Theory]
    [InlineData("approved", "low", "warning")]
    [InlineData("deferred", "low", "warning")]
    [InlineData("pending", "insufficient_data", "insufficient_data")]
    [InlineData("closed", "insufficient_data", "insufficient_data")]
    [InlineData(null, "insufficient_data", "insufficient_data")]
    public void ResolveInventoryBoardConfidence_DoesNotOverstateWorkflowStatus(
        string? status,
        string expectedLevel,
        string expectedDq)
    {
        var resolved = DecisionBoardEndpoints.ResolveInventoryBoardConfidence(status);
        Assert.Equal(expectedLevel, resolved.Level);
        Assert.Equal(expectedDq, resolved.DataQualityStatus);
        Assert.Contains("confidence_workflow_status_only", resolved.WarningCodes);
        Assert.NotEqual("medium", resolved.Level);
        Assert.NotEqual("high", resolved.Level);
    }

    [Fact]
    public void BuildDecisionBoardResponse_InventoryApprovedCard_StaysLowConfidenceWithoutEvidenceScore()
    {
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var productDecisionCenter = CreateProductDecisionCenter(generatedAtUtc);
        var workflow = new InventoryActionWorkflowDto(
            GeneratedAtUtc: generatedAtUtc,
            PendingCount: 0,
            ApprovedCount: 1,
            DeferredCount: 0,
            ClosedCount: 0,
            Items:
            [
                new InventoryActionSuggestionDto(
                    SuggestionKey: "dopuna:sku-1",
                    ActionType: "dopuna",
                    Priority: "critical",
                    Label: "Dopuna test",
                    Reason: "Ispod minimuma.",
                    Status: "approved",
                    ArtikalId: 11,
                    PLU: "SKU-1",
                    Naziv: "Test artikal",
                    FromStoreName: "Store A",
                    ToStoreName: null,
                    SuggestedQty: 3,
                    EstimatedValue: 50_000m,
                    DaysSinceMovement: 5,
                    Note: null,
                    UpdatedAtUtc: generatedAtUtc)
            ]);

        var response = DecisionBoardEndpoints.BuildDecisionBoardResponse(
            generatedAtUtc,
            productDecisionCenter.PeriodFromUtc,
            productDecisionCenter.PeriodToUtc,
            lastRefreshAtUtc: generatedAtUtc,
            productDecisionCenter,
            inventoryInsights: null,
            inventoryWorkflow: workflow,
            supplierSummary: null,
            actions: [],
            outcomeSummary: null,
            refreshStatus: null,
            dataQualityHealth: null,
            loadWarnings: [],
            dataScope: "all",
            storeId: null,
            supplierId: null);

        var inventoryCard = Assert.Single(
            response.Sections
                .SelectMany(section => section.Cards)
                .Where(card => card.Kind == "inventory")
                .DistinctBy(card => card.Id));

        Assert.Equal("low", inventoryCard.ConfidenceLevel);
        Assert.Null(inventoryCard.ConfidenceScore);
        Assert.Equal("warning", inventoryCard.DataQualityStatus);
        Assert.Contains("confidence_workflow_status_only", inventoryCard.WarningCodes);
        Assert.NotEqual("medium", inventoryCard.ConfidenceLevel);
    }

    private static DecisionBoardAggregateResponseDto BuildBoard(
        DateTime generatedAtUtc,
        ProductDecisionCenterResponseDto productDecisionCenter,
        SummaryResponse? supplierSummary = null,
        IReadOnlyList<string>? loadWarnings = null) =>
        DecisionBoardEndpoints.BuildDecisionBoardResponse(
            generatedAtUtc,
            productDecisionCenter.PeriodFromUtc,
            productDecisionCenter.PeriodToUtc,
            lastRefreshAtUtc: generatedAtUtc,
            productDecisionCenter,
            inventoryInsights: null,
            inventoryWorkflow: null,
            supplierSummary: supplierSummary,
            actions: [],
            outcomeSummary: null,
            refreshStatus: null,
            dataQualityHealth: null,
            loadWarnings: loadWarnings ?? [],
            dataScope: "all",
            storeId: null,
            supplierId: null);

    private static IEnumerable<DecisionBoardCardDto> FindProductCards(DecisionBoardAggregateResponseDto response) =>
        response.Sections
            .SelectMany(section => section.Cards)
            .Where(card => card.Kind == "product")
            .DistinctBy(card => card.Id);

    private static SummaryResponse CreateSupplierSummary(
        DateTime generatedAtUtc,
        bool recommendationAllowed,
        bool usedFallback,
        IReadOnlyList<SummarySupplierItem> grow,
        IReadOnlyList<SummarySupplierItem> risk)
    {
        var from = generatedAtUtc.AddDays(-30);
        var trust = new ScorecardTrustMetadata(
            RequestedFrom: from,
            RequestedTo: generatedAtUtc,
            EffectiveFrom: from,
            EffectiveTo: generatedAtUtc,
            RequestedDataset: "supplier_scorecard_v2",
            EffectiveDataset: usedFallback ? "supplier_scorecard_fallback" : "supplier_scorecard_v2",
            EffectivePeriodLabel: "requested",
            DataCoverageStatus: recommendationAllowed ? "good" : "warning",
            UsedFallback: usedFallback,
            FallbackReason: usedFallback ? "Pomoćni dataset aktivan." : null,
            FallbackReasonCode: usedFallback ? "fallback_dataset" : null,
            LastRefreshAtUtc: generatedAtUtc,
            RowCount: grow.Count + risk.Count,
            IgnoredRowCount: 0,
            ZeroRevenueRowsExcludedCount: 0,
            MissingSupplierNameCount: 0,
            HasData: true,
            HasExplicitDateRange: true,
            RecommendationAllowed: recommendationAllowed,
            NoSilentFallback: true,
            WindowDays: 30,
            DataScope: "all",
            Coverage: "full",
            DataNote: recommendationAllowed ? null : "Preporuke su blokirane.");

        return new SummaryResponse(
            From: from,
            To: generatedAtUtc,
            SupplierCount: grow.Count + risk.Count,
            FullPriceRevenueShare: 0.8m,
            FullPriceSellthrough: 0.5m,
            MarkdownRevenueShare: 0.2m,
            PreMarkdownMarginPct: 0.3m,
            CapitalAtRisk: 10_000m,
            TopGrowSuppliers: grow,
            TopRiskSuppliers: risk,
            KeyInsights: [],
            DataNote: trust.DataNote,
            TrustMetadata: trust);
    }

    private static SummarySupplierItem CreateSupplierItem(
        int supplierId,
        string supplierName,
        decimal revenue,
        decimal confidenceScore,
        string recommendationCode) =>
        new(
            SupplierId: supplierId,
            SupplierName: supplierName,
            Revenue: revenue,
            MlSupplierScore: 80m,
            SupplierQualityIndex: 75m,
            RecommendationCode: recommendationCode,
            ConfidenceScore: confidenceScore,
            ReliabilityPct: confidenceScore,
            DataQualityStatus: "good",
            StatusReason: "Test supplier status.",
            ReasonCodes: ["test"]);

    private static ProductDecisionCenterResponseDto CreateProductDecisionCenter(
        DateTime generatedAtUtc,
        params ProductDecisionCenterRowDto[] rows) =>
        new()
        {
            GeneratedAtUtc = generatedAtUtc,
            PeriodFromUtc = generatedAtUtc.AddDays(-30),
            PeriodToUtc = generatedAtUtc,
            Summary = new ProductDecisionCenterSummaryDto(),
            Rows = rows.ToList(),
            Meta = new AnalyticsResponseMetaDto
            {
                Success = true,
                DataQualityStatus = rows.Any(row =>
                    string.Equals(row.DataQualityStatus, "insufficient_data", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.DataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase))
                    ? "insufficient_data"
                    : "good",
                GeneratedAtUtc = generatedAtUtc
            }
        };

    private static ProductDecisionCenterRowDto CreateProductRow(
        int productId,
        string recommendationStatus,
        string dataQualityStatus,
        string confidenceLevel,
        int confidenceScore,
        decimal lostSalesEstimate = 0m,
        decimal slowStockCapital = 0m,
        decimal? expectedImpactRsd = null,
        bool recommendationAllowed = true,
        IEnumerable<string>? warningCodes = null,
        IEnumerable<string>? reasonCodes = null) =>
        new()
        {
            ProductId = productId,
            RecommendationId = $"reco-{productId}",
            SourceType = "product",
            SourceKey = $"product:{productId}",
            RecommendationType = "decision",
            Sku = $"SKU-{productId}",
            ProductName = $"Test proizvod {productId}",
            Revenue = 125_000m,
            UnitsSold = 12,
            VelocityUnitsPerDay = 0.4m,
            MarginContribution = 12_500m,
            MarginPct = 18m,
            MarginCoveragePct = 80m,
            CurrentStock = 4,
            MinStock = 6,
            StockGap = 2,
            LostSalesEstimate = lostSalesEstimate,
            SlowStockCapital = slowStockCapital,
            SignalConfidencePct = confidenceScore,
            RecommendationAllowed = recommendationAllowed,
            DataQualityStatus = dataQualityStatus,
            ConfidenceLevel = confidenceLevel,
            ConfidenceScore = confidenceScore,
            ConfidencePct = confidenceScore,
            ReliabilityPct = Math.Min(confidenceScore + 5, 100),
            RecommendationStatus = recommendationStatus,
            RecommendationLabel = recommendationStatus,
            RecommendationReason = $"Razlog za {recommendationStatus}.",
            ReasonCodes = reasonCodes?.ToList() ?? ["ok"],
            WarningCodes = warningCodes?.ToList() ?? [],
            PrimaryDrivers = ["test"],
            ExpectedImpactRsd = expectedImpactRsd,
            RiskIfIgnored = "Rizik ako se ignoriše.",
            ExplainabilityText = "Test objašnjenje.",
            InputFreshnessStatus = "fresh",
            RecommendedAction = "Test akcija."
        };
}
