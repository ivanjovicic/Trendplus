using Application.Analytics;
using Domain.Model.Analytics;
using Infrastructure.Services.Analytics;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class DecisionBoardAggregationContractTests
{
    private static readonly DateTime GeneratedAtUtc = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodFromUtc = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodToUtc = new(2026, 7, 1, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void BuildResponse_MapsProductImpactAndExactActionStateWithoutCrossTypeCollision()
    {
        var productResponse = BuildProductResponse(
            ProductRow(
                productId: 101,
                sourceKey: "shared:decision:101",
                expectedImpactRsd: 80_000m,
                confidenceLevel: "high",
                confidenceScore: 92,
                dataQualityStatus: "good",
                recommendationStatus: "REPLENISH",
                warningCodes: ["low_stock"]),
            ProductRow(
                productId: 102,
                sourceKey: "product:102",
                expectedImpactRsd: null,
                confidenceLevel: "insufficient_data",
                confidenceScore: null,
                dataQualityStatus: "insufficient_data",
                recommendationStatus: "INSUFFICIENT_DATA",
                warningCodes: ["insufficient_history"]));

        var actions = new List<AnalyticsActionItem>
        {
            Action(
                id: 1,
                sourceType: "supplier",
                sourceKey: "shared:decision:101",
                status: AnalyticsActionConstants.Statuses.Accepted),
            Action(
                id: 2,
                sourceType: "product",
                sourceKey: "product:102",
                status: AnalyticsActionConstants.Statuses.Done)
        };

        var response = Build(productResponse, actions: actions);

        var allCards = response.Sections.SelectMany(section => section.Cards).ToList();
        var highImpactProduct = Assert.Single(
            allCards
                .Where(card => card.Kind == "product" && card.SourceKey == "shared:decision:101")
                .GroupBy(card => card.Id)
                .Select(group => group.First()));
        var closedProduct = Assert.Single(
            allCards
                .Where(card => card.Kind == "product" && card.SourceKey == "product:102")
                .GroupBy(card => card.Id)
                .Select(group => group.First()));

        Assert.False(highImpactProduct.AlreadyInAction);
        Assert.False(highImpactProduct.AlreadyClosed);
        Assert.Equal(80_000m, highImpactProduct.ExpectedImpactRsd);
        Assert.Equal("high", highImpactProduct.ConfidenceLevel);

        Assert.False(closedProduct.AlreadyInAction);
        Assert.True(closedProduct.AlreadyClosed);
        Assert.Null(closedProduct.ExpectedImpactRsd);
        Assert.Equal("insufficient_data", closedProduct.ConfidenceLevel);

        var impactSection = Assert.Single(response.Sections.Where(section => section.Key == "impact"));
        Assert.Contains(impactSection.Cards, card => card.SourceKey == "shared:decision:101");
        Assert.DoesNotContain(impactSection.Cards, card => card.SourceKey == "product:102");
    }

    [Fact]
    public void BuildResponse_OpenProductActionMarksRecommendationAndAppearsInActionDecisionSection()
    {
        var productResponse = BuildProductResponse(
            ProductRow(
                productId: 201,
                sourceKey: "product:201",
                expectedImpactRsd: 45_000m,
                confidenceLevel: "high",
                confidenceScore: 88,
                dataQualityStatus: "good",
                recommendationStatus: "BOOST"));

        var actions = new List<AnalyticsActionItem>
        {
            Action(
                id: 11,
                sourceType: "product",
                sourceKey: "product:201",
                status: AnalyticsActionConstants.Statuses.Accepted,
                expectedImpactRsd: 45_000m)
        };

        var response = Build(productResponse, actions: actions);

        var productCard = response.Sections
            .SelectMany(section => section.Cards)
            .First(card => card.Kind == "product" && card.SourceKey == "product:201");
        Assert.True(productCard.AlreadyInAction);
        Assert.False(productCard.AlreadyClosed);

        var actionSection = Assert.Single(response.Sections.Where(section => section.Key == "actionsDecision"));
        var actionCard = Assert.Single(actionSection.Cards);
        Assert.Equal("action", actionCard.Kind);
        Assert.Equal("product:201", actionCard.SourceKey);
        Assert.True(actionCard.AlreadyInAction);
        Assert.Equal(45_000m, actionCard.ExpectedImpactRsd);
    }

    [Fact]
    public void BuildResponse_EmptySourcesReturnsExplicitNoBoardDataMeta()
    {
        var response = Build(productDecisionCenter: null);

        Assert.Equal(7, response.Sections.Count);
        Assert.All(response.Sections, section => Assert.Empty(section.Cards));
        Assert.False(response.Sections.SelectMany(section => section.Cards).Any());

        Assert.NotNull(response.Meta);
        Assert.True(response.Meta!.Success);
        Assert.Equal("no_board_data", response.Meta.EmptyReason);
        Assert.Equal("insufficient_data", response.Meta.DataQualityStatus);
        Assert.False(response.Meta.IsPartial);
    }

    [Fact]
    public void BuildResponse_LoadWarningProducesPartialMetaWithoutDiscardingValidCards()
    {
        var productResponse = BuildProductResponse(
            ProductRow(
                productId: 301,
                sourceKey: "product:301",
                expectedImpactRsd: 25_000m,
                confidenceLevel: "medium",
                confidenceScore: 70,
                dataQualityStatus: "warning",
                recommendationStatus: "WATCH"));

        var response = Build(
            productResponse,
            loadWarnings: ["inventory_insights_unavailable"]);

        Assert.Contains(response.Sections.SelectMany(section => section.Cards), card => card.SourceKey == "product:301");
        Assert.Contains(response.Warnings, warning => warning.Contains("inventory_insights_unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(response.Meta);
        Assert.True(response.Meta!.Success);
        Assert.True(response.Meta.IsPartial);
        Assert.Equal("BOARD_PARTIAL", response.Meta.WarningCode);
    }

    [Fact]
    public void BuildResponse_OutcomeSummaryWithInsufficientSampleKeepsSampleContextButNotDecisionConfidence()
    {
        var response = Build(
            productDecisionCenter: null,
            outcomeSummary: OutcomeSummary(measuredSampleSize: 3, sampleSize: 8));

        var outcomeCard = Assert.Single(response.Sections.Single(section => section.Key == "actionsOutcome").Cards);

        Assert.Equal("insufficient_data", outcomeCard.ConfidenceLevel);
        Assert.Null(outcomeCard.ConfidenceScore);
        Assert.Contains("Uzorak: 3", outcomeCard.Summary);

        var blockerCard = Assert.Single(response.Sections.Single(section => section.Key == "blockers").Cards);
        Assert.Equal("blocker-outcome-sample", blockerCard.Id);
        Assert.Null(blockerCard.ConfidenceScore);
    }

    [Fact]
    public void BuildResponse_UrgentSectionIsDeterministicUniqueAndCappedAtFive()
    {
        var rows = Enumerable.Range(1, 10)
            .Select(index => ProductRow(
                productId: 400 + index,
                sourceKey: $"product:{400 + index}",
                expectedImpactRsd: index * 10_000m,
                confidenceLevel: "high",
                confidenceScore: 80 + index,
                dataQualityStatus: "good",
                recommendationStatus: "REPLENISH",
                warningCodes: ["low_stock"]))
            .ToArray();

        var response = Build(BuildProductResponse(rows));
        var urgent = Assert.Single(response.Sections.Where(section => section.Key == "urgent"));

        Assert.Equal(5, urgent.Cards.Count);
        Assert.Equal(5, urgent.Cards.Select(card => card.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            urgent.Cards.OrderByDescending(card => card.PriorityScore).Select(card => card.Id).ToArray(),
            urgent.Cards.Select(card => card.Id).ToArray());
        Assert.Equal(urgent.Cards.Max(card => card.PriorityScore), urgent.Cards[0].PriorityScore);
        Assert.Contains(urgent.Cards, card => card.SourceKey == "product:410");
    }

    [Fact]
    public void BuildResponse_InsufficientDataNeverEntersHighImpactSectionThroughFakeZero()
    {
        var response = Build(BuildProductResponse(
            ProductRow(
                productId: 501,
                sourceKey: "product:501",
                expectedImpactRsd: null,
                confidenceLevel: "insufficient_data",
                confidenceScore: null,
                dataQualityStatus: "insufficient_data",
                recommendationStatus: "INSUFFICIENT_DATA",
                warningCodes: ["missing_cost", "insufficient_history"])));

        var urgent = Assert.Single(response.Sections.Where(section => section.Key == "urgent"));
        var card = Assert.Single(urgent.Cards);
        Assert.True(card.PriorityScore <= 40m);
        Assert.Null(card.ExpectedImpactRsd);

        var impact = Assert.Single(response.Sections.Where(section => section.Key == "impact"));
        Assert.Empty(impact.Cards);
    }

    [Fact]
    public void BuildResponse_FixDataDoesNotPromoteLostSalesEstimateToExpectedImpact()
    {
        var response = Build(BuildProductResponse(
            ProductRow(
                productId: 601,
                sourceKey: "product:601",
                expectedImpactRsd: null,
                confidenceLevel: "insufficient_data",
                confidenceScore: null,
                dataQualityStatus: "critical",
                recommendationStatus: "FIX_DATA",
                warningCodes: ["missing_cost"],
                lostSalesEstimate: 42_000m,
                recommendationLabel: "Proveri podatke",
                recommendedAction: "Ispravi dobavljača, nabavnu cenu ili kategoriju pre odluke.")));

        var card = Assert.Single(
            response.Sections
                .SelectMany(section => section.Cards)
                .Where(item => item.Kind == "product")
                .GroupBy(item => item.Id)
                .Select(group => group.First()));

        Assert.Null(card.ExpectedImpactRsd);
        Assert.Equal(0m, card.ImpactScore);
        Assert.DoesNotContain("FIX_DATA", card.RecommendedNextAction, StringComparison.Ordinal);
        Assert.Equal("Ispravi dobavljača, nabavnu cenu ili kategoriju pre odluke.", card.RecommendedNextAction);

        var impact = Assert.Single(response.Sections.Where(section => section.Key == "impact"));
        Assert.Empty(impact.Cards);
    }

    [Fact]
    public void BuildResponse_EmptyPdcRows_IsSuccessfulEmptyBoard_NotFakeHealthyZeroImpact()
    {
        var emptyPdc = new ProductDecisionCenterResponseDto
        {
            GeneratedAtUtc = GeneratedAtUtc,
            PeriodFromUtc = PeriodFromUtc,
            PeriodToUtc = PeriodToUtc,
            TotalRows = 0,
            AnalyzedRows = 0,
            Summary = new ProductDecisionCenterSummaryDto(),
            Rows = [],
            Meta = new AnalyticsResponseMetaDto
            {
                Success = true,
                DataQualityStatus = "insufficient_data",
                EmptyReason = "no_rows_for_period",
                GeneratedAtUtc = GeneratedAtUtc
            }
        };

        var response = Build(emptyPdc);

        Assert.Equal(7, response.Sections.Count);
        Assert.All(response.Sections, section => Assert.Empty(section.Cards));
        Assert.NotNull(response.Meta);
        Assert.True(response.Meta!.Success);
        Assert.Equal("no_board_data", response.Meta.EmptyReason);
        Assert.Equal("insufficient_data", response.Meta.DataQualityStatus);
        Assert.False(response.Meta.IsPartial);
        Assert.DoesNotContain(
            response.Sections.SelectMany(section => section.Cards),
            card => card.ExpectedImpactRsd == 0m);
    }

    [Fact]
    public void BuildResponse_DoesNotSurfaceStatusFieldsAsWarningCodes()
    {
        var actions = new List<AnalyticsActionItem>
        {
            Action(
                id: 21,
                sourceType: "product",
                sourceKey: "product:201",
                status: AnalyticsActionConstants.Statuses.Accepted,
                expectedImpactRsd: 12_000m,
                dataQualityStatus: "warning",
                outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Pending)
        };

        var response = DecisionBoardEndpoints.BuildDecisionBoardResponse(
            generatedAtUtc: GeneratedAtUtc,
            periodFromUtc: PeriodFromUtc,
            periodToUtc: PeriodToUtc,
            lastRefreshAtUtc: GeneratedAtUtc,
            productDecisionCenter: null,
            inventoryInsights: null,
            inventoryWorkflow: null,
            supplierSummary: null,
            actions,
            outcomeSummary: null,
            refreshStatus: new AnalyticsRefreshStatusDto
            {
                DataFreshnessStatus = "stale",
                GeneratedAtUtc = GeneratedAtUtc
            },
            dataQualityHealth: null,
            loadWarnings: [],
            dataScope: "all",
            storeId: null,
            supplierId: null);

        var actionCard = Assert.Single(response.Sections.Single(section => section.Key == "actionsDecision").Cards);
        Assert.Equal("warning", actionCard.DataQualityStatus);
        Assert.DoesNotContain("warning", actionCard.WarningCodes);

        var outcomeCard = Assert.Single(response.Sections.Single(section => section.Key == "actionsOutcome").Cards);
        Assert.DoesNotContain(AnalyticsActionConstants.OutcomeStatuses.Pending, outcomeCard.WarningCodes);

        var refreshCard = Assert.Single(response.Sections.Single(section => section.Key == "blockers").Cards.Where(card => card.Id == "blocker-refresh"));
        Assert.Equal("stale", refreshCard.DataQualityStatus);
        Assert.DoesNotContain("stale", refreshCard.WarningCodes);
    }

    private static DecisionBoardAggregateResponseDto Build(
        ProductDecisionCenterResponseDto? productDecisionCenter,
        IReadOnlyList<AnalyticsActionItem>? actions = null,
        IReadOnlyList<string>? loadWarnings = null,
        AnalyticsActionOutcomeSummaryDto? outcomeSummary = null)
    {
        return DecisionBoardEndpoints.BuildDecisionBoardResponse(
            generatedAtUtc: GeneratedAtUtc,
            periodFromUtc: PeriodFromUtc,
            periodToUtc: PeriodToUtc,
            lastRefreshAtUtc: GeneratedAtUtc,
            productDecisionCenter,
            inventoryInsights: null,
            inventoryWorkflow: null,
            supplierSummary: null,
            actions: actions ?? [],
            outcomeSummary: outcomeSummary,
            refreshStatus: null,
            dataQualityHealth: null,
            loadWarnings: loadWarnings ?? [],
            dataScope: "all",
            storeId: null,
            supplierId: null);
    }

    private static ProductDecisionCenterResponseDto BuildProductResponse(params ProductDecisionCenterRowDto[] rows)
    {
        return new ProductDecisionCenterResponseDto
        {
            GeneratedAtUtc = GeneratedAtUtc,
            PeriodFromUtc = PeriodFromUtc,
            PeriodToUtc = PeriodToUtc,
            TotalRows = rows.Length,
            AnalyzedRows = rows.Length,
            Summary = new ProductDecisionCenterSummaryDto
            {
                ReplenishCount = rows.Count(row => row.RecommendationStatus == "REPLENISH"),
                MarkdownCount = rows.Count(row => row.RecommendationStatus == "MARKDOWN"),
                HighPotentialCount = rows.Count(row => row.RecommendationStatus == "BOOST"),
                BadDataCount = rows.Count(row => row.RecommendationStatus == "FIX_DATA"),
                LostSalesEstimate = rows.Sum(row => row.LostSalesEstimate),
                SlowStockCapital = rows.Sum(row => row.SlowStockCapital)
            },
            Rows = rows.ToList(),
            Meta = new AnalyticsResponseMetaDto
            {
                Success = true,
                DataQualityStatus = rows.Any(row => row.DataQualityStatus == "insufficient_data")
                    ? "insufficient_data"
                    : rows.Any(row => row.DataQualityStatus == "warning") ? "warning" : "good",
                GeneratedAtUtc = GeneratedAtUtc,
                LastRefreshAtUtc = GeneratedAtUtc
            }
        };
    }

    private static ProductDecisionCenterRowDto ProductRow(
        int productId,
        string sourceKey,
        decimal? expectedImpactRsd,
        string confidenceLevel,
        int? confidenceScore,
        string dataQualityStatus,
        string recommendationStatus,
        IReadOnlyList<string>? warningCodes = null,
        decimal? lostSalesEstimate = null,
        string? recommendationLabel = null,
        string? recommendedAction = null,
        bool recommendationAllowed = true)
    {
        return new ProductDecisionCenterRowDto
        {
            ProductId = productId,
            RecommendationId = $"recommendation:{productId}",
            SourceType = "product",
            SourceKey = sourceKey,
            RecommendationType = recommendationStatus,
            Sku = $"SKU-{productId}",
            ProductName = $"Model {productId}",
            Revenue = expectedImpactRsd ?? 0m,
            UnitsSold = confidenceScore.HasValue ? 20 : 0,
            VelocityUnitsPerDay = confidenceScore.HasValue ? 1.2m : 0m,
            MarginContribution = expectedImpactRsd ?? 0m,
            MarginPct = confidenceScore.HasValue ? 25m : null,
            MarginCoveragePct = confidenceScore.HasValue ? 90m : 0m,
            CurrentStock = recommendationStatus == "REPLENISH" ? 0 : 10,
            MinStock = 5,
            StockGap = recommendationStatus == "REPLENISH" ? 5 : 0,
            DaysSinceLastSale = confidenceScore.HasValue ? 2 : null,
            TrendPct = confidenceScore.HasValue ? 12m : null,
            LostSalesEstimate = lostSalesEstimate ?? expectedImpactRsd ?? 0m,
            SlowStockCapital = 0m,
            DataQualityStatus = dataQualityStatus,
            ConfidenceLevel = confidenceLevel,
            ConfidenceScore = confidenceScore,
            ConfidencePct = confidenceScore ?? 0,
            ReliabilityPct = confidenceScore ?? 0,
            RecommendationAllowed = recommendationAllowed,
            RecommendationStatus = recommendationStatus,
            RecommendationLabel = recommendationLabel ?? recommendationStatus,
            RecommendationReason = $"Razlog za {productId}",
            ReasonCodes = warningCodes?.ToList() ?? [],
            WarningCodes = warningCodes?.ToList() ?? [],
            PrimaryDrivers = confidenceScore.HasValue ? ["sales_velocity", "stock_risk"] : ["sparse_sales"],
            ExpectedImpactRsd = expectedImpactRsd,
            ImpactWindowDays = expectedImpactRsd.HasValue ? 14 : null,
            RiskIfIgnored = $"Rizik za {productId}",
            ExplainabilityText = $"Objašnjenje za {productId}",
            InputFreshnessStatus = confidenceScore.HasValue ? "fresh" : "critical",
            RecommendedAction = recommendedAction
                ?? (recommendationStatus == "REPLENISH" ? "Dopuni" : "Proveri")
        };
    }

    private static AnalyticsActionItem Action(
        long id,
        string sourceType,
        string sourceKey,
        string status,
        decimal? expectedImpactRsd = null,
        string? dataQualityStatus = null,
        string? outcomeStatus = null)
    {
        return new AnalyticsActionItem
        {
            Id = id,
            SourceType = sourceType,
            SourceKey = sourceKey,
            SourceId = checked((int)id),
            Title = $"Akcija {id}",
            Description = $"Opis akcije {id}",
            RecommendationStatus = "REPLENISH",
            Priority = AnalyticsActionConstants.Priorities.P1,
            Status = status,
            ExpectedImpactRsd = expectedImpactRsd,
            DataQualityStatus = dataQualityStatus,
            OutcomeStatus = outcomeStatus,
            CreatedAtUtc = GeneratedAtUtc.AddDays(-2),
            UpdatedAtUtc = GeneratedAtUtc.AddDays(-1),
            DueAtUtc = GeneratedAtUtc.AddDays(3)
        };
    }

    private static AnalyticsActionOutcomeSummaryDto OutcomeSummary(
        int measuredSampleSize,
        int sampleSize,
        decimal? expectedImpactRsd = 12_000m,
        decimal? measuredImpactRsd = 4_000m,
        decimal? outcomeCoverageRate = 0.3750m,
        IReadOnlyList<string>? warnings = null)
    {
        return new AnalyticsActionOutcomeSummaryDto(
            Meta: new AnalyticsActionOutcomeSummaryMetaDto(
                Success: true,
                PeriodMode: "resolved",
                CreatedFrom: PeriodFromUtc,
                CreatedTo: PeriodToUtc,
                ResolvedFrom: PeriodFromUtc,
                ResolvedTo: PeriodToUtc,
                MeasuredFrom: PeriodFromUtc,
                MeasuredTo: PeriodToUtc,
                GeneratedAtUtc: GeneratedAtUtc,
                SampleSize: sampleSize,
                MeasuredSampleSize: measuredSampleSize,
                Warnings: warnings ?? [],
                EmptyReason: null),
            Totals: new AnalyticsActionOutcomeSummaryTotalsDto(
                CreatedCount: sampleSize,
                ClosedCount: sampleSize,
                OpenCount: 0,
                MeasuredCount: measuredSampleSize,
                MeasuredOutcomeCount: measuredSampleSize,
                PendingOutcomeCount: Math.Max(sampleSize - measuredSampleSize, 0),
                SuccessCount: measuredSampleSize,
                NeutralCount: 0,
                NegativeCount: 0,
                NotMeasuredCount: Math.Max(sampleSize - measuredSampleSize, 0),
                OutcomeCoverageRate: outcomeCoverageRate,
                PositiveOutcomeRate: measuredSampleSize > 0 ? 1.0000m : null,
                NegativeOutcomeRate: measuredSampleSize > 0 ? 0.0000m : null,
                ClosedOutcomeCoverageRate: outcomeCoverageRate,
                MeasuredPositiveOutcomeRate: measuredSampleSize > 0 ? 1.0000m : null,
                MeasuredNegativeOutcomeRate: measuredSampleSize > 0 ? 0.0000m : null),
            Impact: new AnalyticsActionOutcomeSummaryImpactDto(
                ExpectedImpactRsd: expectedImpactRsd,
                MeasuredImpactRsd: measuredImpactRsd,
                RealizationRatio: expectedImpactRsd.HasValue && expectedImpactRsd != 0m && measuredImpactRsd.HasValue
                    ? measuredImpactRsd.Value / expectedImpactRsd.Value
                    : null,
                MeasuredImpactSampleCount: measuredSampleSize),
            BySourceType: [],
            ByPriority: [],
            ByOutcomeStatus: [],
            ByDataQuality: [],
            ByConfidenceBucket: [],
            ByReliabilityBucket: []);
    }
}
