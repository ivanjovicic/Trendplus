using Api.Config;
using Api.Services;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Domain.Model.Analytics;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Analytics;
using Infrastructure.Services.Caching;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Trendplus2.Dtos;

namespace Trendplus2.Endpoints;

public static class DecisionBoardEndpoints
{
    private const int DefaultLookbackDays = 180;

    public static void MapDecisionBoardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/decision-board", HandleDecisionBoardAsync)
            .WithName("GetDecisionBoard")
            .WithTags("Analytics")
            .RequireRateLimiting("analytics");
    }

    internal static async Task<IResult> HandleDecisionBoardAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        IAnalyticsCacheService cache,
        ITrendplusDbContext trendDb,
        IAnalyticsDbContext analyticsDb,
        AnalyticsActionItemService actionItemService,
        AnalyticsRefreshStatusService refreshStatusService,
        AnalyticsDataQualityHealthService dataQualityHealthService,
        IInventoryActionDecisionService inventoryActionDecisionService,
        ILogger<Program> logger,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? storeId = null,
        int? supplierId = null,
        string? dataScope = null,
        string? category = null,
        string? gender = null,
        int? seasonId = null,
        decimal? minRevenue = null,
        bool onlyHighConfidence = false,
        bool excludeOosBeforeMarkdown = false,
        string? search = null,
        CancellationToken ct = default)
    {
        var correlationId = ResolveCorrelationId(httpContext);
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var (periodFromUtc, periodToUtc) = NormalizeDecisionWindow(fromDate, toDate);
        var warnings = new List<string>();

        ProductDecisionCenterResponseDto? productDecisionCenter = null;
        InventoryInsightsDto? inventoryInsights = null;
        InventoryActionWorkflowDto? inventoryWorkflow = null;
        SummaryResponse? supplierSummary = null;
        IReadOnlyList<AnalyticsActionItem> actions = [];
        AnalyticsActionOutcomeSummaryDto? outcomeSummary = null;
        AnalyticsRefreshStatusDto? refreshStatus = null;
        AnalyticsDataQualityHealthSnapshot? dataQualityHealth = null;

        try
        {
            productDecisionCenter = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
                trendDb,
                periodFromUtc,
                periodToUtc,
                storeId,
                supplierId,
                300,
                normalizedDataScope,
                ct);
        }
        catch (Exception ex)
        {
            warnings.Add("product_decision_center_unavailable");
            logger.LogWarning(ex, "Decision board product snapshot failed.");
        }

        try
        {
            inventoryInsights = await InventoryEndpoints.GetInventoryInsightsAsync(
                cache,
                trendDb,
                analyticsDb,
                storeId,
                supplierId,
                search,
                null,
                ct,
                normalizedDataScope);
        }
        catch (Exception ex)
        {
            warnings.Add("inventory_insights_unavailable");
            logger.LogWarning(ex, "Decision board inventory insights failed.");
        }

        try
        {
            inventoryWorkflow = await InventoryEndpoints.GetInventoryActionWorkflowAsync(
                cache,
                trendDb,
                analyticsDb,
                inventoryActionDecisionService,
                storeId,
                supplierId,
                search,
                ct,
                normalizedDataScope);
        }
        catch (Exception ex)
        {
            warnings.Add("inventory_workflow_unavailable");
            logger.LogWarning(ex, "Decision board inventory workflow failed.");
        }

        try
        {
            var analyticsConnectionString = AnalyticsConnectionResolver.Resolve(configuration);
            if (SupplierDecisionHubEndpoints.TryCreateFilters(
                    periodFromUtc,
                    periodToUtc,
                    category,
                    gender,
                    seasonId,
                    minRevenue,
                    onlyHighConfidence,
                    excludeOosBeforeMarkdown,
                    supplierId,
                    storeId,
                    normalizedDataScope,
                    out var supplierFilters,
                    out var validationError))
            {
                var dataset = await SupplierDecisionHubEndpoints.GetSupplierRowsCachedAsync(
                    cache,
                    analyticsConnectionString,
                    supplierFilters!,
                    ct);
                supplierSummary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, supplierFilters!);
            }
            else
            {
                warnings.Add("supplier_filters_invalid");
                logger.LogWarning("Decision board supplier filters are invalid: {ValidationError}", validationError);
            }
        }
        catch (Exception ex)
        {
            warnings.Add("supplier_summary_unavailable");
            logger.LogWarning(ex, "Decision board supplier summary failed.");
        }

        try
        {
            var (items, _) = await actionItemService.ListAsync(
                status: null,
                priority: null,
                sourceType: null,
                dataQualityStatus: null,
                search: null,
                page: 1,
                pageSize: 500,
                ct);
            actions = items;
            outcomeSummary = await actionItemService.GetOutcomeSummaryAsync(
                new AnalyticsActionOutcomeSummaryQuery(
                    CreatedFrom: periodFromUtc,
                    CreatedTo: periodToUtc,
                    ResolvedFrom: periodFromUtc,
                    ResolvedTo: periodToUtc,
                    MeasuredFrom: periodFromUtc,
                    MeasuredTo: periodToUtc,
                    SourceType: null,
                    Priority: null,
                    DataQualityStatus: null),
                ct);
        }
        catch (Exception ex)
        {
            warnings.Add("analytics_actions_unavailable");
            logger.LogWarning(ex, "Decision board actions snapshot failed.");
        }

        try
        {
            refreshStatus = await refreshStatusService.GetStatusAsync(ct);
        }
        catch (Exception ex)
        {
            warnings.Add("refresh_status_unavailable");
            logger.LogWarning(ex, "Decision board refresh status failed.");
        }

        try
        {
            var lookbackDays = Math.Clamp((int)Math.Ceiling((periodToUtc.Date - periodFromUtc.Date).TotalDays) + 1, 1, 365);
            dataQualityHealth = await dataQualityHealthService.CaptureAsync(lookbackDays, normalizedDataScope, ct);
        }
        catch (Exception ex)
        {
            warnings.Add("data_quality_health_unavailable");
            logger.LogWarning(ex, "Decision board data quality snapshot failed.");
        }

        var response = BuildDecisionBoardResponse(
            generatedAtUtc: DateTime.UtcNow,
            periodFromUtc,
            periodToUtc,
            refreshStatus?.LastSuccessfulRefreshAtUtc ?? productDecisionCenter?.GeneratedAtUtc,
            productDecisionCenter,
            inventoryInsights,
            inventoryWorkflow,
            supplierSummary,
            actions,
            outcomeSummary,
            refreshStatus,
            dataQualityHealth,
            warnings,
            normalizedDataScope,
            storeId,
            supplierId);

        var responseMeta = BuildDecisionBoardMeta(response, warnings, correlationId);
        return Results.Ok(response with { Meta = responseMeta });
    }

    internal static DecisionBoardAggregateResponseDto BuildDecisionBoardResponse(
        DateTime generatedAtUtc,
        DateTime? periodFromUtc,
        DateTime? periodToUtc,
        DateTime? lastRefreshAtUtc,
        ProductDecisionCenterResponseDto? productDecisionCenter,
        InventoryInsightsDto? inventoryInsights,
        InventoryActionWorkflowDto? inventoryWorkflow,
        SummaryResponse? supplierSummary,
        IReadOnlyList<AnalyticsActionItem> actions,
        AnalyticsActionOutcomeSummaryDto? outcomeSummary,
        AnalyticsRefreshStatusDto? refreshStatus,
        AnalyticsDataQualityHealthSnapshot? dataQualityHealth,
        IReadOnlyList<string> loadWarnings,
        string dataScope,
        int? storeId,
        int? supplierId)
    {
        var actionStateByKey = BuildActionStateMap(actions);
        var openActions = actions.Where(item => IsOpenStatus(item.Status)).ToList();

        var productCards = BuildProductCards(productDecisionCenter, actionStateByKey);
        var inventoryCards = BuildInventoryCards(inventoryWorkflow, actionStateByKey);
        var supplierCards = BuildSupplierCards(supplierSummary, actionStateByKey, dataScope);
        var actionCards = BuildActionCards(openActions);
        var outcomeCards = BuildOutcomeCards(outcomeSummary, actions);
        var blockerCards = BuildBlockerCards(refreshStatus, dataQualityHealth, supplierSummary, outcomeSummary);

        var urgentCards = CombineSectionCards(
            "urgent",
            5,
            blockerCards,
            productCards,
            inventoryCards,
            supplierCards.Where(IsActionableSupplierDecisionCard),
            actionCards,
            outcomeCards);

        var impactCards = CombineSectionCards(
            "impact",
            5,
            productCards.Where(card => (card.ExpectedImpactRsd ?? 0m) > 0m),
            inventoryCards.Where(card => (card.ExpectedImpactRsd ?? 0m) > 0m),
            supplierCards.Where(IsActionableSupplierDecisionCard),
            actionCards.Where(card => (card.ExpectedImpactRsd ?? 0m) > 0m));

        var stockRiskCards = CombineSectionCards(
            "stockRisk",
            5,
            inventoryCards,
            productCards.Where(card => card.WarningCodes.Any(code => IsStockWarning(code) || IsStockWarning(card.Title) || IsStockWarning(card.RiskIfIgnored) || (card.ExpectedImpactRsd ?? 0m) > 0m)));

        var supplierRiskCards = CombineSectionCards(
            "supplierRisk",
            5,
            supplierCards,
            actionCards.Where(card => string.Equals(card.SourceType, "supplier", StringComparison.OrdinalIgnoreCase)));

        var blockerSectionCards = CombineSectionCards("blockers", 5, blockerCards);
        var actionDecisionCards = CombineSectionCards("actionsDecision", 5, actionCards);
        var actionOutcomeCards = CombineSectionCards("actionsOutcome", 5, outcomeCards);

        var sections = new List<DecisionBoardSectionDto>
        {
            new("urgent", "Top 5 urgentnih odluka", "Najpre obrati pažnju na sigurnost, blokere i najveći signal koji je već spreman za odluku.", "/analytics", "Trenutno nema dovoljno jakih odluka za ovu sekciju.", BuildSectionWarnings(urgentCards), urgentCards),
            new("impact", "Najveći očekivani uticaj", "Gde je očekivani poslovni efekat najveći ako tim odmah reaguje.", "/analytics/products", "Nema kandidata sa procenjenim uticajem u ovom preseku.", BuildSectionWarnings(impactCards), impactCards),
            new("stockRisk", "Odluke o riziku zaliha", "Dopuna, rasprodaja, spor obrt i prekomerna zaliha moraju biti vidljivi na jednom mestu.", "/analytics/inventory", "Nema jasnih signala za zalihe u ovom trenutku.", BuildSectionWarnings(stockRiskCards), stockRiskCards),
            new("supplierRisk", "Odluke o riziku i prilici kod dobavljača", "Dobavljači sa jakim signalom, ali i oni sa rizikom, treba da budu poređani zajedno.", "/analytics/supplier?tab=overview", "Nema dobavljača sa dovoljno jakim signalom za ovaj pogled.", BuildSectionWarnings(supplierRiskCards), supplierRiskCards),
            new("blockers", "Blokatori kvaliteta podataka", "Kada su podaci slabi, odluke moraju biti eksplicitno blokirane ili upozorene.", "/analytics/data-quality", "Nema aktivnih blokatora u izabranim izvorima.", BuildSectionWarnings(blockerSectionCards), blockerSectionCards),
            new("actionsDecision", "Akcije koje čekaju odluku", "Otvorene akcije još čekaju odluku i ne treba da nestanu iz fokusa.", "/analytics/actions", "Nema otvorenih akcija koje čekaju odluku.", BuildSectionWarnings(actionDecisionCards), actionDecisionCards),
            new("actionsOutcome", "Akcije koje čekaju ishod", "Zatvorene akcije bez merenja su feedback gap, ne failure.", "/analytics/actions", "Nema akcija koje čekaju ishod.", BuildSectionWarnings(actionOutcomeCards), actionOutcomeCards)
        };

        var overallDataQualityStatus = DeriveWorstStatus([
            refreshStatus?.DataFreshnessStatus,
            dataQualityHealth is null ? null : EvaluateDataQualityHealth(dataQualityHealth).Status,
            productDecisionCenter?.Meta?.DataQualityStatus,
            supplierSummary?.TrustMetadata?.DataCoverageStatus,
            outcomeSummary?.Meta.Warnings.Count > 0 ? "warning" : null
        ]);

        var metrics = new List<DecisionBoardMetricDto>
        {
            new("Urgentne odluke", urgentCards.Count.ToString(CultureInfo.InvariantCulture), urgentCards.Count > 0 ? "critical" : "good"),
            new("Visok uticaj", impactCards.Count.ToString(CultureInfo.InvariantCulture), impactCards.Count > 0 ? "warning" : "neutral"),
            new("Blokatori", blockerSectionCards.Count.ToString(CultureInfo.InvariantCulture), blockerSectionCards.Count > 0 ? "critical" : "good"),
            new("Otvorene akcije", actionDecisionCards.Count.ToString(CultureInfo.InvariantCulture), actionDecisionCards.Count > 0 ? "warning" : "good"),
            new("Ishodi na čekanju", actionOutcomeCards.Count.ToString(CultureInfo.InvariantCulture), actionOutcomeCards.Count > 0 ? "warning" : "good"),
            new("Pouzdani produkt signali", productCards.Count(card => card.ConfidenceLevel == "high").ToString(CultureInfo.InvariantCulture), productCards.Any(card => card.ConfidenceLevel == "high") ? "good" : "neutral")
        };

        var sourceStates = BuildSourceStates(
            productDecisionCenter,
            inventoryInsights,
            inventoryWorkflow,
            supplierSummary,
            actions,
            outcomeSummary,
            refreshStatus,
            dataQualityHealth,
            loadWarnings);

        var warnings = BuildWarnings(
            loadWarnings,
            sourceStates,
            overallDataQualityStatus,
            sections);

        var recommendationNote = supplierSummary?.TrustMetadata?.UsedFallback == true
            ? supplierSummary.TrustMetadata.FallbackReason ?? "Supplier signal koristi pomoćni dataset."
            : "Backend ostaje izvor istine; board samo kompozira postojeće signale.";

        var hasData = sections.SelectMany(section => section.Cards).Any();
        var response = new DecisionBoardAggregateResponseDto(
            generatedAtUtc,
            periodFromUtc,
            periodToUtc,
            lastRefreshAtUtc,
            overallDataQualityStatus ?? "unknown",
            recommendationNote,
            warnings,
            metrics,
            sourceStates,
            sections,
            hasData
                ? AnalyticsResponseMetaFactory.Success(overallDataQualityStatus ?? "good", lastRefreshAtUtc, warnings.Count > 0, warnings.Count > 0 ? "BOARD_PARTIAL" : null, warnings.Count > 0 ? "Deo izvora za board je trenutno nedostupan." : null)
                : AnalyticsResponseMetaFactory.Empty("no_board_data", "Nema dovoljno signala za izvršni board.", "insufficient_data"));

        return response;
    }

    private static AnalyticsResponseMetaDto BuildDecisionBoardMeta(
        DecisionBoardAggregateResponseDto response,
        List<string> loadWarnings,
        string correlationId)
    {
        var meta = response.Meta ?? AnalyticsResponseMetaFactory.Success(
            response.OverallDataQualityStatus,
            response.LastRefreshAtUtc,
            response.Warnings.Count > 0,
            response.Warnings.Count > 0 ? "BOARD_PARTIAL" : null,
            response.Warnings.Count > 0 ? "Deo izvora za board je trenutno nedostupan." : null);

        meta.CorrelationId = correlationId;
        if (loadWarnings.Count > 0 && meta.WarningCode is null)
        {
            meta.WarningCode = "BOARD_PARTIAL";
        }

        return meta;
    }

    private static List<DecisionBoardCardDto> BuildProductCards(
        ProductDecisionCenterResponseDto? productDecisionCenter,
        IReadOnlyDictionary<string, ActionState> actionStates)
    {
        if (productDecisionCenter is null || productDecisionCenter.Rows.Count == 0)
        {
            return [];
        }

        return productDecisionCenter.Rows
            .Select((row, index) =>
            {
                var recommendationAllowed = IsProductRecommendationAllowed(row);
                var confidence = ResolveProductConfidence(row, recommendationAllowed);
                // Trust PDC: do not reattach LostSalesEstimate when ExpectedImpactRsd is intentionally null.
                var expectedImpact = recommendationAllowed ? row.ExpectedImpactRsd : null;
                var warnings = NormalizeWarningCodes(row.WarningCodes).ToList();
                if (!recommendationAllowed)
                {
                    warnings.Add("product_recommendation_blocked");
                }
                var actionState = ResolveActionState(row.SourceType ?? "product", row.SourceKey ?? $"product:{row.ProductId}", actionStates);

                return new DecisionBoardCardDto(
                    Id: $"product:{row.ProductId}:{index}",
                    Kind: "product",
                    SectionKey: "urgent",
                    SourceModule: "Odluke o proizvodima",
                    SourceType: row.SourceType ?? "product",
                    SourceKey: row.SourceKey ?? $"product:{row.ProductId}",
                    Title: row.ProductName,
                    Summary: row.ExplainabilityText ?? row.RecommendationReason,
                    ConfidenceLevel: confidence.Level,
                    ConfidenceScore: confidence.Score,
                    ReliabilityPct: row.ReliabilityPct,
                    ExpectedImpactRsd: expectedImpact,
                    MeasuredImpactRsd: null,
                    RealizationRatio: null,
                    RiskIfIgnored: row.RiskIfIgnored ?? row.RecommendationReason,
                    // A blocked signal must not rank or quantify an action, but its
                    // source-owned remediation can still tell the user what evidence to fix.
                    RecommendedNextAction: row.RecommendedAction,
                    ActionHref: "/analytics/products",
                    AlreadyInAction: actionState == ActionState.Open,
                    AlreadyClosed: actionState == ActionState.Closed,
                    WarningCodes: warnings.Distinct(StringComparer.Ordinal).ToList(),
                    ConfidenceSource: ResolveProductConfidenceSource(row),
                    ReasonCodes: row.ReasonCodes,
                    RecommendationAllowed: recommendationAllowed,
                    DataQualityStatus: NormalizeDataQualityStatus(row.DataQualityStatus),
                    GeneratedAtUtc: productDecisionCenter.GeneratedAtUtc,
                    PriorityScore: CapInsufficientDataPriority(
                        ComputePriorityScore(expectedImpact, confidence.Score, row.DataQualityStatus, row.RecommendationStatus),
                        confidence.Level,
                        row.DataQualityStatus),
                    ImpactScore: expectedImpact ?? 0m);
            })
            .OrderByDescending(card => card.PriorityScore)
            .Take(12)
            .ToList();
    }

    private static List<DecisionBoardCardDto> BuildInventoryCards(
        InventoryActionWorkflowDto? inventoryWorkflow,
        IReadOnlyDictionary<string, ActionState> actionStates)
    {
        if (inventoryWorkflow is null || inventoryWorkflow.Items.Count == 0)
        {
            return [];
        }

        return inventoryWorkflow.Items
            .Select((item, index) =>
            {
                var sourceKey = $"inventory:{item.SuggestionKey}";
                var actionState = ResolveActionState("inventory", sourceKey, actionStates);
                var confidence = ResolveInventoryBoardConfidence(item);
                var confidenceSource = item.SignalConfidencePct.HasValue ? "signal" : "workflow_status_only";
                var reasonCodes = item.SignalReasonCodes ?? [];
                var priorityScore = item.Priority switch
                {
                    "critical" => 250m,
                    "high" => 210m,
                    "medium" => 160m,
                    _ => 100m
                } + Math.Min(item.EstimatedValue / 5_000m, 50m) - Math.Min(item.DaysSinceMovement / 5m, 30m);

                return new DecisionBoardCardDto(
                    Id: $"inventory:{item.SuggestionKey}:{index}",
                    Kind: "inventory",
                    SectionKey: "stockRisk",
                    SourceModule: "Zalihe",
                    SourceType: "inventory",
                    SourceKey: sourceKey,
                    Title: item.Label,
                    Summary: item.Reason,
                    ConfidenceLevel: confidence.Level,
                    ConfidenceScore: confidence.ConfidenceScore,
                    ReliabilityPct: confidence.ConfidenceScore.HasValue
                        ? (int?)Math.Clamp((int)Math.Round(confidence.ConfidenceScore.Value, MidpointRounding.AwayFromZero), 0, 100)
                        : null,
                    ExpectedImpactRsd: item.EstimatedValue > 0 ? item.EstimatedValue : null,
                    MeasuredImpactRsd: null,
                    RealizationRatio: null,
                    RiskIfIgnored: item.Reason,
                    RecommendedNextAction: item.Label,
                    ActionHref: "/analytics/inventory",
                    AlreadyInAction: actionState is ActionState.Open,
                    AlreadyClosed: actionState is ActionState.Closed,
                    WarningCodes: confidence.WarningCodes,
                    DataQualityStatus: confidence.DataQualityStatus,
                    GeneratedAtUtc: inventoryWorkflow.GeneratedAtUtc,
                    PriorityScore: CapInsufficientDataPriority(
                        priorityScore,
                        confidence.Level,
                        confidence.DataQualityStatus),
                    ImpactScore: item.EstimatedValue,
                    ConfidenceSource: confidenceSource,
                    ReasonCodes: reasonCodes,
                    RecommendationAllowed: item.RecommendationAllowed);
            })
            .OrderByDescending(card => card.PriorityScore)
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Uses signal evidence from workflow DTO when present; otherwise workflow-status fallback (RQ10).
    /// See docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md.
    /// </summary>
    internal static (string Level, string DataQualityStatus, IReadOnlyList<string> WarningCodes, decimal? ConfidenceScore)
        ResolveInventoryBoardConfidence(InventoryActionSuggestionDto item)
    {
        if (item.SignalConfidencePct.HasValue)
        {
            var score = item.SignalConfidencePct.Value;
            var warnings = new List<string>();
            if (item.SignalReasonCodes is { Count: > 0 })
            {
                warnings.AddRange(item.SignalReasonCodes.Where(code => !string.IsNullOrWhiteSpace(code)));
            }

            if (item.RecommendationAllowed == false)
            {
                warnings.Add("inventory_recommendation_blocked");
                var blockedDq = NormalizeDataQualityStatus(item.SignalDataQualityStatus);
                return (
                    "insufficient_data",
                    blockedDq == "good" ? "warning" : blockedDq,
                    warnings.Distinct(StringComparer.Ordinal).ToList(),
                    null);
            }

            var level = ResolveConfidenceLevel(score);
            var dataQuality = NormalizeDataQualityStatus(item.SignalDataQualityStatus);
            if (string.Equals(dataQuality, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                dataQuality = string.Equals(level, "insufficient_data", StringComparison.OrdinalIgnoreCase)
                    ? "insufficient_data"
                    : "warning";
            }

            return (level, dataQuality, warnings.Distinct(StringComparer.Ordinal).ToList(), score);
        }

        return ResolveInventoryBoardConfidenceFromWorkflow(item.Status);
    }

    internal static (string Level, string DataQualityStatus, IReadOnlyList<string> WarningCodes, decimal? ConfidenceScore)
        ResolveInventoryBoardConfidenceFromWorkflow(string? workflowStatus)
    {
        var status = (workflowStatus ?? string.Empty).Trim().ToLowerInvariant();
        var warningCodes = (IReadOnlyList<string>)["confidence_workflow_status_only"];

        return status switch
        {
            "approved" or "deferred" => ("low", "warning", warningCodes, null),
            _ => ("insufficient_data", "insufficient_data", warningCodes, null)
        };
    }

    private static List<DecisionBoardCardDto> BuildSupplierCards(
        SummaryResponse? supplierSummary,
        IReadOnlyDictionary<string, ActionState> actionStates,
        string dataScope)
    {
        if (supplierSummary is null)
        {
            return [];
        }

        var trust = supplierSummary.TrustMetadata;
        // Trust metadata is optional for compatibility, but an absent contract cannot authorize a supplier decision.
        var recommendationAllowed = trust?.RecommendationAllowed ?? false;
        var filters = new
        {
            FromDate = supplierSummary.From,
            ToDate = supplierSummary.To,
            StoreId = (int?)null,
            DataScope = trust?.DataScope ?? dataScope
        };

        var cards = new List<DecisionBoardCardDto>();
        var supplierGroups = new[]
        {
            ("Dobavljači za širenje saradnje", supplierSummary.TopGrowSuppliers, "grow"),
            ("Dobavljači sa rizikom", supplierSummary.TopRiskSuppliers, "risk")
        };

        foreach (var (title, items, sourceTag) in supplierGroups)
        {
            foreach (var (item, index) in items.Select((value, idx) => (value, idx)))
            {
                var actionKey = BuildSupplierActionSourceKey(item, filters.FromDate, filters.ToDate, filters.StoreId, filters.DataScope, recommendationAllowed);
                var actionState = ResolveActionState("supplier", actionKey, actionStates);
                var confidenceScore = item.ConfidenceScore;
                // Blocked recommendations are verification signals, not actionable decisions.
                var confidenceLevel = recommendationAllowed
                    ? ResolveConfidenceLevel(confidenceScore)
                    : "insufficient_data";
                var dataQualityStatus = recommendationAllowed
                    ? (trust?.DataCoverageStatus ?? "unknown")
                    : "insufficient_data";
                var summary = recommendationAllowed
                    ? $"{item.RecommendationCode}. {item.StatusReason}"
                    : $"Signal check: {item.RecommendationCode}. Preporuka nije dozvoljena. {item.StatusReason}";
                var nextAction = recommendationAllowed
                    ? item.RecommendationCode
                    : "Proveri pouzdanost dobavljačkog dataset-a pre odluke.";

                cards.Add(new DecisionBoardCardDto(
                    Id: $"supplier:{sourceTag}:{item.SupplierId}:{index}",
                    Kind: "supplier",
                    SectionKey: "supplierRisk",
                    SourceModule: "Dobavljači",
                    SourceType: "supplier",
                    SourceKey: actionKey,
                    Title: item.SupplierName,
                    Summary: summary,
                    ConfidenceLevel: confidenceLevel,
                    ConfidenceScore: confidenceScore,
                    ReliabilityPct: item.ReliabilityPct,
                    ExpectedImpactRsd: null,
                    MeasuredImpactRsd: null,
                    RealizationRatio: null,
                    RiskIfIgnored: item.StatusReason,
                    RecommendedNextAction: nextAction,
                    ActionHref: "/analytics/supplier?tab=overview",
                    AlreadyInAction: actionState == ActionState.Open,
                    AlreadyClosed: actionState == ActionState.Closed,
                    WarningCodes: BuildSupplierWarningCodes(trust),
                    ReasonCodes: item.ReasonCodes,
                    DataQualityStatus: dataQualityStatus,
                    // The supplier period is not a generation timestamp. If the
                    // refresh lineage is unknown, use the response generation
                    // time rather than presenting a historical period boundary.
                    GeneratedAtUtc: trust?.LastRefreshAtUtc ?? supplierSummary.Meta?.GeneratedAtUtc ?? DateTime.UtcNow,
                    PriorityScore: CapInsufficientDataPriority(
                        ComputeSupplierPriority(item, trust, recommendationAllowed),
                        confidenceLevel,
                        dataQualityStatus),
                    ImpactScore: recommendationAllowed ? item.Revenue : 0m,
                    RecommendationAllowed: recommendationAllowed));
            }
        }

        return cards
            .OrderByDescending(card => card.PriorityScore)
            .Take(10)
            .ToList();
    }

    private static List<DecisionBoardCardDto> BuildActionCards(List<AnalyticsActionItem> openActions)
    {
        if (openActions.Count == 0)
        {
            return [];
        }

        return openActions
            .OrderByDescending(item => PriorityRank(item.Priority))
            .ThenBy(item => item.DueAtUtc ?? DateTime.MaxValue)
            .Take(12)
            .Select((item, index) =>
            {
                var expectedImpact = item.ExpectedImpactRsd ?? item.ImpactEstimateRsd;
                var confidenceLevel = ResolveConfidenceLevel(item.ConfidencePct);
                return new DecisionBoardCardDto(
                    Id: $"action:{item.Id}:{index}",
                    Kind: "action",
                    SectionKey: "actionsDecision",
                    SourceModule: "Centralne akcije",
                    SourceType: item.SourceType,
                    SourceKey: item.SourceKey,
                    Title: item.Title,
                    Summary: item.Description ?? item.RecommendationStatus ?? "Otvorena akcija još čeka odluku.",
                    ConfidenceLevel: confidenceLevel,
                    ConfidenceScore: item.ConfidencePct,
                    ReliabilityPct: item.ReliabilityPct,
                    ExpectedImpactRsd: expectedImpact,
                    MeasuredImpactRsd: item.MeasuredImpactRsd,
                    RealizationRatio: null,
                    RiskIfIgnored: item.Description ?? "Akcija još nije zatvorena.",
                    RecommendedNextAction: item.Status == AnalyticsActionConstants.Statuses.New
                        ? "Prihvati ili odbij preporuku."
                        : item.Status == AnalyticsActionConstants.Statuses.Accepted
                            ? "Prati sprovođenje i zabeleži ishod."
                            : "Ponovo proceni prioritet i rok.",
                    ActionHref: "/analytics/actions",
                    AlreadyInAction: true,
                    AlreadyClosed: false,
                    WarningCodes: [],
                    DataQualityStatus: NormalizeDataQualityStatus(item.DataQualityStatus),
                    GeneratedAtUtc: item.UpdatedAtUtc,
                    PriorityScore: ComputePriorityScore(expectedImpact, item.ConfidencePct, item.DataQualityStatus, item.RecommendationStatus),
                    ImpactScore: expectedImpact ?? 0m);
            })
            .ToList();
    }

    private static List<DecisionBoardCardDto> BuildOutcomeCards(
        AnalyticsActionOutcomeSummaryDto? outcomeSummary,
        IReadOnlyList<AnalyticsActionItem> actions)
    {
        if (outcomeSummary is null && actions.Count == 0)
        {
            return [];
        }

        var cards = new List<DecisionBoardCardDto>();

        if (outcomeSummary is not null)
        {
            var warningCodes = outcomeSummary.Meta.Warnings ?? [];
            var confidenceLevel = outcomeSummary.Meta.MeasuredSampleSize < 10
                ? "insufficient_data"
                : warningCodes.Count > 0
                    ? "low"
                    : "medium";

            cards.Add(new DecisionBoardCardDto(
                Id: "outcome-summary",
                Kind: "outcome",
                SectionKey: "actionsOutcome",
                SourceModule: "Sažetak ishoda",
                SourceType: "action_outcome",
                SourceKey: "action_outcome:summary",
                Title: "Realizacija očekivanog uticaja",
                Summary: $"Izmereno: {FmtRsd(outcomeSummary.Impact.MeasuredImpactRsd)} · Očekivano: {FmtRsd(outcomeSummary.Impact.ExpectedImpactRsd)} · Coverage: {FmtPct(outcomeSummary.Totals.OutcomeCoverageRate)} · Uzorak: {outcomeSummary.Meta.MeasuredSampleSize}.",
                ConfidenceLevel: confidenceLevel,
                ConfidenceScore: null,
                ReliabilityPct: null,
                ExpectedImpactRsd: outcomeSummary.Impact.ExpectedImpactRsd,
                MeasuredImpactRsd: outcomeSummary.Impact.MeasuredImpactRsd,
                RealizationRatio: outcomeSummary.Impact.RealizationRatio,
                RiskIfIgnored: warningCodes.Count > 0 ? "Uzorak ishoda je još mali ili nepotpun." : "Feedback loop je otvoren i treba ga pratiti.",
                RecommendedNextAction: "Uporedi očekivani i izmereni uticaj pre daljeg širenja preporuka.",
                ActionHref: "/analytics/actions",
                AlreadyInAction: false,
                AlreadyClosed: false,
                WarningCodes: warningCodes,
                DataQualityStatus: confidenceLevel == "insufficient_data" ? "insufficient_data" : warningCodes.Count > 0 ? "warning" : "good",
                GeneratedAtUtc: outcomeSummary.Meta.GeneratedAtUtc,
                PriorityScore: (outcomeSummary.Impact.ExpectedImpactRsd ?? 0m) / 5_000m + (warningCodes.Count > 0 ? 30m : 0m),
                ImpactScore: outcomeSummary.Impact.ExpectedImpactRsd ?? 0m));
        }

        foreach (var item in actions.Where(static item => IsOpenOutcomeStatus(item.OutcomeStatus) || item.OutcomeStatus is "pending" or "not_measured").Take(10))
        {
            var expectedImpact = item.ExpectedImpactRsd ?? item.ImpactEstimateRsd;
            cards.Add(new DecisionBoardCardDto(
                Id: $"outcome:{item.Id}",
                Kind: "outcome",
                SectionKey: "actionsOutcome",
                SourceModule: "Ishodi akcija",
                SourceType: item.SourceType,
                SourceKey: item.SourceKey,
                Title: item.Title,
                Summary: item.OutcomeNotes ?? item.Description ?? "Ishod još nije izmeren.",
                ConfidenceLevel: ResolveConfidenceLevel(item.ConfidencePct),
                ConfidenceScore: item.ConfidencePct,
                ReliabilityPct: item.ReliabilityPct,
                ExpectedImpactRsd: expectedImpact,
                MeasuredImpactRsd: item.MeasuredImpactRsd,
                RealizationRatio: null,
                RiskIfIgnored: item.OutcomeNotes ?? "Ovaj ishod još ne može da se koristi za učenje.",
                RecommendedNextAction: "Zabeleži ili validiraj ishod.",
                ActionHref: "/analytics/actions",
                AlreadyInAction: false,
                AlreadyClosed: true,
                WarningCodes: [],
                DataQualityStatus: NormalizeDataQualityStatus(item.DataQualityStatus),
                GeneratedAtUtc: item.UpdatedAtUtc,
                PriorityScore: ComputePriorityScore(expectedImpact, item.ConfidencePct, item.DataQualityStatus, item.RecommendationStatus) - 20m,
                ImpactScore: expectedImpact ?? 0m));
        }

        return cards
            .OrderByDescending(card => card.PriorityScore)
            .ToList();
    }

    private static List<DecisionBoardCardDto> BuildBlockerCards(
        AnalyticsRefreshStatusDto? refreshStatus,
        AnalyticsDataQualityHealthSnapshot? dataQualityHealth,
        SummaryResponse? supplierSummary,
        AnalyticsActionOutcomeSummaryDto? outcomeSummary)
    {
        var cards = new List<DecisionBoardCardDto>();

        if (refreshStatus is not null && (IsStale(refreshStatus.DataFreshnessStatus)))
        {
            cards.Add(new DecisionBoardCardDto(
                Id: "blocker-refresh",
                Kind: "blocker",
                SectionKey: "blockers",
                SourceModule: "Pilot spremnost",
                SourceType: "refresh",
                SourceKey: "refresh-status",
                Title: "Osvežavanje je zastarelo",
                Summary: refreshStatus.LastErrorMessage ?? "Poslednje osvežavanje je zastarelo ili kritično.",
                ConfidenceLevel: "insufficient_data",
                ConfidenceScore: null,
                ReliabilityPct: null,
                ExpectedImpactRsd: null,
                MeasuredImpactRsd: null,
                RealizationRatio: null,
                RiskIfIgnored: "Board ne treba da izgleda sveže dok worker ne vrati poslednji uspešan refresh.",
                RecommendedNextAction: "Proveri worker panel i pokreni osvežavanje ako je bezbedno.",
                ActionHref: "/admin/configuration?panel=workers",
                AlreadyInAction: false,
                AlreadyClosed: false,
                WarningCodes: [],
                DataQualityStatus: refreshStatus.DataFreshnessStatus,
                GeneratedAtUtc: refreshStatus.GeneratedAtUtc,
                PriorityScore: 300m,
                ImpactScore: 0m));
        }

        if (dataQualityHealth is not null)
        {
            var dataQualityState = EvaluateDataQualityHealth(dataQualityHealth);

            if (dataQualityState.Status is not "good" and not "excellent")
            {
            cards.Add(new DecisionBoardCardDto(
                Id: "blocker-health",
                Kind: "blocker",
                SectionKey: "blockers",
                SourceModule: "Kvalitet podataka",
                SourceType: "data_quality",
                SourceKey: "data-quality-health",
                Title: "Data quality health traži proveru",
                Summary: dataQualityState.Summary,
                ConfidenceLevel: "insufficient_data",
                ConfidenceScore: dataQualityState.Score,
                ReliabilityPct: null,
                ExpectedImpactRsd: dataQualityHealth.MissingCostRevenue > 0 ? dataQualityHealth.MissingCostRevenue : null,
                MeasuredImpactRsd: null,
                RealizationRatio: null,
                RiskIfIgnored: "Slab data quality direktno spušta pouzdanost preporuka u board-u.",
                RecommendedNextAction: "Otvori kvalitet podataka i reši najskuplje blokere.",
                ActionHref: "/analytics/data-quality",
                AlreadyInAction: false,
                AlreadyClosed: false,
                WarningCodes: BuildHealthWarningCodes(dataQualityHealth),
                DataQualityStatus: dataQualityState.Status,
                GeneratedAtUtc: dataQualityHealth.GeneratedAtUtc,
                PriorityScore: dataQualityState.Status == "critical" ? 280m : 190m,
                ImpactScore: dataQualityHealth.MissingCostRevenue));
            }
        }

        if (supplierSummary is { TrustMetadata: null })
        {
            cards.Add(new DecisionBoardCardDto(
                Id: "blocker-supplier-trust-missing",
                Kind: "blocker",
                SectionKey: "blockers",
                SourceModule: "Dobavljači",
                SourceType: "supplier",
                SourceKey: "supplier-trust",
                Title: "Nedostaju trust metapodaci dobavljača",
                Summary: "Supplier rezultati su vraćeni bez metapodataka koji potvrđuju kvalitet i dozvolu preporuke.",
                ConfidenceLevel: "insufficient_data",
                ConfidenceScore: null,
                ReliabilityPct: null,
                ExpectedImpactRsd: null,
                MeasuredImpactRsd: null,
                RealizationRatio: null,
                RiskIfIgnored: "Dobavljačka preporuka može delovati akcijski bez dokaza da je dozvoljena.",
                RecommendedNextAction: "Otvori dobavljački report i proveri trust metapodatke pre odluke.",
                ActionHref: "/analytics/supplier?tab=overview",
                AlreadyInAction: false,
                AlreadyClosed: false,
                WarningCodes: BuildSupplierWarningCodes(null),
                DataQualityStatus: "insufficient_data",
                GeneratedAtUtc: null,
                PriorityScore: 175m,
                ImpactScore: 0m));
        }
        else if (supplierSummary?.TrustMetadata is { RecommendationAllowed: false })
        {
            cards.Add(new DecisionBoardCardDto(
                Id: "blocker-supplier-trust",
                Kind: "blocker",
                SectionKey: "blockers",
                SourceModule: "Dobavljači",
                SourceType: "supplier",
                SourceKey: "supplier-trust",
                Title: "Supplier signal je pomoćni",
                Summary: supplierSummary.TrustMetadata.DataNote ?? supplierSummary.DataNote ?? "Preporuke kod dobavljača koriste pomoćni dataset.",
                ConfidenceLevel: "insufficient_data",
                ConfidenceScore: null,
                ReliabilityPct: null,
                ExpectedImpactRsd: null,
                MeasuredImpactRsd: null,
                RealizationRatio: null,
                RiskIfIgnored: supplierSummary.TrustMetadata.FallbackReason ?? "Rezultati dobavljača ostaju ograničeni dok je fallback aktivan.",
                RecommendedNextAction: "Otvori dobavljački report i proveri dataset/coverage.",
                ActionHref: "/analytics/supplier?tab=overview",
                AlreadyInAction: false,
                AlreadyClosed: false,
                WarningCodes: BuildSupplierWarningCodes(supplierSummary.TrustMetadata),
                DataQualityStatus: supplierSummary.TrustMetadata.DataCoverageStatus,
                GeneratedAtUtc: supplierSummary.TrustMetadata.LastRefreshAtUtc,
                PriorityScore: 175m,
                ImpactScore: 0m));
        }

        if (outcomeSummary is not null && outcomeSummary.Meta.MeasuredSampleSize < 10)
        {
            cards.Add(new DecisionBoardCardDto(
                Id: "blocker-outcome-sample",
                Kind: "blocker",
                SectionKey: "blockers",
                SourceModule: "Ishodi akcija",
                SourceType: "action_outcome",
                SourceKey: "action_outcome:sample",
                Title: "Ishod uzorak je još mali",
                Summary: "Feedback loop ima mali ili nepotpun uzorak i ne sme da izgleda kao završena evaluacija.",
                ConfidenceLevel: "insufficient_data",
                ConfidenceScore: null,
                ReliabilityPct: null,
                ExpectedImpactRsd: outcomeSummary.Impact.ExpectedImpactRsd,
                MeasuredImpactRsd: outcomeSummary.Impact.MeasuredImpactRsd,
                RealizationRatio: outcomeSummary.Impact.RealizationRatio,
                RiskIfIgnored: "Preporuke ne treba da se kalibrišu na premalom uzorku.",
                RecommendedNextAction: "Nastavi merenje ishoda i zatvori gap u uzorku.",
                ActionHref: "/analytics/actions",
                AlreadyInAction: false,
                AlreadyClosed: false,
                WarningCodes: outcomeSummary.Meta.Warnings.ToList(),
                DataQualityStatus: "insufficient_data",
                GeneratedAtUtc: outcomeSummary.Meta.GeneratedAtUtc,
                PriorityScore: 160m,
                ImpactScore: outcomeSummary.Impact.ExpectedImpactRsd ?? 0m));
        }

        return cards
            .OrderByDescending(card => card.PriorityScore)
            .ToList();
    }

    private static List<DecisionBoardCardDto> CombineSectionCards(
        string sectionKey,
        int maxItems,
        params IEnumerable<DecisionBoardCardDto>[] sources)
    {
        var cards = new List<DecisionBoardCardDto>();
        foreach (var source in sources)
        {
            foreach (var card in source)
            {
                cards.Add(card with { SectionKey = sectionKey });
            }
        }

        return cards
            .OrderByDescending(card => card.PriorityScore)
            .ThenByDescending(card => card.ImpactScore)
            .Take(maxItems)
            .ToList();
    }

    private static List<string> BuildSectionWarnings(IReadOnlyList<DecisionBoardCardDto> cards)
        => cards.SelectMany(card => card.WarningCodes).Distinct(StringComparer.Ordinal).Take(8).ToList();

    private static IReadOnlyList<DecisionBoardSourceStateDto> BuildSourceStates(
        ProductDecisionCenterResponseDto? productDecisionCenter,
        InventoryInsightsDto? inventoryInsights,
        InventoryActionWorkflowDto? inventoryWorkflow,
        SummaryResponse? supplierSummary,
        IReadOnlyList<AnalyticsActionItem> actions,
        AnalyticsActionOutcomeSummaryDto? outcomeSummary,
        AnalyticsRefreshStatusDto? refreshStatus,
        AnalyticsDataQualityHealthSnapshot? dataQualityHealth,
        IReadOnlyList<string> loadWarnings)
    {
        var actionsUnavailable = loadWarnings.Contains("analytics_actions_unavailable", StringComparer.Ordinal);
        var actionsSource = ResolveAnalyticsActionsSourceState(actions, actionsUnavailable);

        return
        [
            new("product-decision-center", "Product Decision Center", ResolveMetaStatus(productDecisionCenter?.Meta?.DataQualityStatus), productDecisionCenter?.GeneratedAtUtc, BuildMetaWarnings(productDecisionCenter?.Meta), productDecisionCenter?.Meta?.Message, "/analytics/products"),
            new("inventory-workflow", "Inventory action workflow", inventoryWorkflow is null ? "unknown" : inventoryWorkflow.PendingCount > 0 ? "warning" : "good", inventoryWorkflow?.GeneratedAtUtc, inventoryWorkflow is null ? ["inventory_workflow_unavailable"] : [], inventoryWorkflow is null ? "Inventory workflow nije dostupan." : null, "/analytics/inventory"),
            new("supplier-decision-hub", "Supplier decision hub", supplierSummary?.TrustMetadata?.DataCoverageStatus ?? "unknown", supplierSummary?.TrustMetadata?.LastRefreshAtUtc, BuildSupplierWarningCodes(supplierSummary?.TrustMetadata), supplierSummary?.DataNote, "/analytics/supplier?tab=overview"),
            new("analytics-actions", "Analytics actions", actionsSource.Status, actionsSource.GeneratedAtUtc, actionsSource.WarningCodes, actionsSource.Message, "/analytics/actions"),
            new("action-outcome-summary", "Action outcome summary", outcomeSummary is null ? "unknown" : outcomeSummary.Meta.MeasuredSampleSize < 10 ? "warning" : "good", outcomeSummary?.Meta.GeneratedAtUtc, outcomeSummary?.Meta.Warnings ?? [], outcomeSummary?.Meta.EmptyReason, "/analytics/actions"),
            new("refresh-status", "Refresh status", refreshStatus?.DataFreshnessStatus ?? "unknown", refreshStatus?.GeneratedAtUtc, BuildRefreshWarnings(refreshStatus), refreshStatus?.LastErrorMessage, "/analytics/pilot-readiness"),
            new("data-quality-health", "Data quality health", dataQualityHealth is null ? "unknown" : EvaluateDataQualityHealth(dataQualityHealth).Status, dataQualityHealth?.GeneratedAtUtc, BuildHealthWarningCodes(dataQualityHealth), dataQualityHealth is null ? null : EvaluateDataQualityHealth(dataQualityHealth).Summary, "/analytics/data-quality")
        ];
    }

    /// <summary>
    /// Empty action list after a successful load is a healthy empty state.
    /// Insufficient data applies only when the actions service failed to load.
    /// </summary>
    internal static (string Status, DateTime? GeneratedAtUtc, IReadOnlyList<string> WarningCodes, string? Message)
        ResolveAnalyticsActionsSourceState(IReadOnlyList<AnalyticsActionItem> actions, bool actionsUnavailable)
    {
        if (actionsUnavailable)
        {
            return (
                "insufficient_data",
                null,
                ["analytics_actions_unavailable"],
                "Lista akcija nije dostupna.");
        }

        if (actions.Count == 0)
        {
            return (
                "good",
                null,
                [],
                "Nema akcija u izabranom kontekstu — prazan rezultat je validan.");
        }

        return (
            "good",
            actions.Max(item => item.UpdatedAtUtc),
            [],
            null);
    }

    private static List<string> BuildWarnings(
        IReadOnlyList<string> loadWarnings,
        IReadOnlyList<DecisionBoardSourceStateDto> sourceStates,
        string? overallDataQualityStatus,
        IReadOnlyList<DecisionBoardSectionDto> sections)
    {
        return loadWarnings
            .Concat(sourceStates.SelectMany(state => state.WarningCodes))
            .Concat(sections.SelectMany(section => section.Warnings))
            .Concat(string.IsNullOrWhiteSpace(overallDataQualityStatus) ? [] : [overallDataQualityStatus!])
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> BuildMetaWarnings(AnalyticsResponseMetaDto? meta)
    {
        if (meta is null)
        {
            return [];
        }

        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(meta.WarningCode))
        {
            warnings.Add(meta.WarningCode!);
        }

        if (meta.IsPartial)
        {
            warnings.Add("partial");
        }

        if (!string.IsNullOrWhiteSpace(meta.DataQualityStatus) && meta.DataQualityStatus != "good")
        {
            warnings.Add(meta.DataQualityStatus!);
        }

        return warnings.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> BuildRefreshWarnings(AnalyticsRefreshStatusDto? refreshStatus)
    {
        if (refreshStatus is null)
        {
            return [];
        }

        var warnings = new List<string>();
        if (IsStale(refreshStatus.DataFreshnessStatus))
        {
            warnings.Add(refreshStatus.DataFreshnessStatus);
        }

        if (!string.IsNullOrWhiteSpace(refreshStatus.LastErrorMessage))
        {
            warnings.Add("refresh_error");
        }

        return warnings.Distinct(StringComparer.Ordinal).ToList();
    }

    internal static (int Score, string Status, string Summary) EvaluateDataQualityHealth(AnalyticsDataQualityHealthSnapshot health)
    {
        static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));

        // No revenue evidence: zero share percentages are not "clean" — they are unknown.
        if (!health.HasRevenueEvidence || health.TotalRevenue <= 0m)
        {
            var orphanNote = health.OrphanArticleCount > 0
                ? $" Orphan artikli i dalje postoje ({health.OrphanArticleCount}), ali bez prometa udele nisu merljive."
                : string.Empty;
            return (
                Score: 0,
                Status: "insufficient_data",
                Summary: $"Nema prometnog dokaza u izabranom periodu; data quality se ne sme tretirati kao dobar.{orphanNote}");
        }

        const double warningOrphanArticleCount = 10d;
        const double warningUnknownSupplierRevenueSharePct = 3d;
        const double warningMissingCostRevenueSharePct = 5d;
        const double scoreMissingCostWeight = 0.5d;
        const double scoreUnknownSupplierWeight = 0.3d;
        const double scoreOrphanWeight = 0.2d;
        const double scorePenaltyAtWarning = 0.45d;
        const double scoreCriticalMultiplier = 3d;

        var missingCostPenalty = CalculatePenalty(
            health.MissingCostRevenueSharePct,
            warningMissingCostRevenueSharePct,
            scorePenaltyAtWarning,
            scoreCriticalMultiplier);
        var unknownSupplierPenalty = CalculatePenalty(
            health.UnknownSupplierRevenueSharePct,
            warningUnknownSupplierRevenueSharePct,
            scorePenaltyAtWarning,
            scoreCriticalMultiplier);
        var orphanPenalty = CalculatePenalty(
            health.OrphanArticleCount,
            warningOrphanArticleCount,
            scorePenaltyAtWarning,
            scoreCriticalMultiplier);

        var weightedPenalty =
            missingCostPenalty * scoreMissingCostWeight +
            unknownSupplierPenalty * scoreUnknownSupplierWeight +
            orphanPenalty * scoreOrphanWeight;

        var score = (int)Math.Round(100d * (1d - Clamp01(weightedPenalty / Math.Max(0.0001d, scoreMissingCostWeight + scoreUnknownSupplierWeight + scoreOrphanWeight))));
        var status = score switch
        {
            >= 90 => "excellent",
            >= 75 => "good",
            >= 50 => "warning",
            _ => "critical"
        };

        var dominantRisk = new[]
        {
            (Label: "missing nabavna cena", Score: missingCostPenalty * scoreMissingCostWeight),
            (Label: "unknown supplier promet", Score: unknownSupplierPenalty * scoreUnknownSupplierWeight),
            (Label: "orphan artikli", Score: orphanPenalty * scoreOrphanWeight),
        }
        .OrderByDescending(item => item.Score)
        .First().Label;

        var summary = status switch
        {
            "excellent" => "Prometni pokazatelji nemaju izmeren rizik u ovom periodu; spremnost za preporuke proverava se odvojeno.",
            "good" => $"Prometni pokazatelji su uglavnom pokriveni. Najveci rizik: {dominantRisk}; proverite spremnost za preporuke.",
            "warning" => $"Prometni pokazatelji imaju vidljive rupe. Najveci rizik: {dominantRisk}; preporuke su ogranicene.",
            _ => $"Prometni podaci traze hitnu korekciju. Najveci rizik: {dominantRisk}; preporuke nisu bezbedne."
        };

        return (score, status, summary);
    }

    private static double CalculatePenalty(double? metricValue, double warningThreshold, double warningPenalty, double criticalMultiplier)
    {
        if (metricValue is null || !double.IsFinite(metricValue.Value) || warningThreshold <= 0d)
        {
            return 0d;
        }

        var normalized = Math.Max(0d, metricValue.Value) / warningThreshold;
        if (normalized <= 1d)
        {
            return normalized * warningPenalty;
        }

        var overflowSpan = Math.Max(0.25d, criticalMultiplier - 1d);
        var overflowProgress = Math.Min((normalized - 1d) / overflowSpan, 1d);
        return warningPenalty + overflowProgress * (1d - warningPenalty);
    }

    private static List<string> BuildHealthWarningCodes(AnalyticsDataQualityHealthSnapshot? health)
    {
        if (health is null)
        {
            return [];
        }

        var warnings = new List<string>();
        var dataQualityState = EvaluateDataQualityHealth(health);
        if (dataQualityState.Status is not "good" and not "excellent")
        {
            warnings.Add(dataQualityState.Status);
        }

        if (!health.HasRevenueEvidence || health.TotalRevenue <= 0m)
        {
            warnings.Add("no_revenue_evidence");
        }

        if (health.MissingCostRevenueSharePct > 0)
        {
            warnings.Add("missing_cost");
        }

        if (health.UnknownSupplierRevenueSharePct > 0)
        {
            warnings.Add("missing_supplier");
        }

        return warnings.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> BuildSupplierWarningCodes(ScorecardTrustMetadata? trust)
    {
        if (trust is null)
        {
            return ["supplier_recommendation_blocked", "supplier_trust_missing"];
        }

        var warnings = new List<string>();
        if (trust.UsedFallback)
        {
            warnings.Add("supplier_fallback");
        }

        if (!trust.RecommendationAllowed)
        {
            warnings.Add("supplier_recommendation_blocked");
        }

        if (!string.IsNullOrWhiteSpace(trust.DataCoverageStatus) && trust.DataCoverageStatus != "good")
        {
            warnings.Add(trust.DataCoverageStatus);
        }

        return warnings.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> NormalizeWarningCodes(IEnumerable<string>? warnings, IEnumerable<string>? fallback = null)
    {
        var values = new List<string>();
        if (warnings is not null)
        {
            values.AddRange(warnings.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        }

        if (fallback is not null)
        {
            values.AddRange(fallback.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        }

        return values.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string ResolveMetaStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? "unknown"
            : status.Trim().ToLowerInvariant();
    }

    private static string NormalizeDataQualityStatus(string? status)
    {
        var normalized = ResolveMetaStatus(status);
        return normalized is "good" or "warning" or "critical" or "insufficient_data" ? normalized : normalized;
    }

    private static string NormalizeDataScope(string? value)
    {
        var normalized = (value ?? "all").Trim().ToLowerInvariant();
        return normalized is "all" or "existing" or "imported" ? normalized : "all";
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeDecisionWindow(DateTime? fromDate, DateTime? toDate)
    {
        var today = DateTime.UtcNow.Date;
        var toUtc = NormalizeDate(toDate) ?? today;
        var fromUtc = NormalizeDate(fromDate) ?? toUtc.AddDays(-(DefaultLookbackDays - 1));

        if (fromUtc > toUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return (fromUtc, toUtc);
    }

    private static DateTime? NormalizeDate(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var normalized = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();

        return normalized.Date;
    }

    private static Dictionary<string, ActionState> BuildActionStateMap(IReadOnlyList<AnalyticsActionItem> actions)
    {
        var states = new Dictionary<string, ActionState>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in actions)
        {
            var key = $"{action.SourceType}:{action.SourceKey}";
            if (IsOpenStatus(action.Status))
            {
                states[key] = ActionState.Open;
                continue;
            }

            if (!states.ContainsKey(key))
            {
                states[key] = ActionState.Closed;
            }
        }

        return states;
    }

    private static ActionState ResolveActionState(string? sourceType, string? sourceKey, IReadOnlyDictionary<string, ActionState> states)
    {
        if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceKey))
        {
            return ActionState.None;
        }

        return states.TryGetValue($"{sourceType}:{sourceKey}", out var state) ? state : ActionState.None;
    }

    private static bool IsOpenStatus(string? status)
        => status is AnalyticsActionConstants.Statuses.New or AnalyticsActionConstants.Statuses.Accepted or AnalyticsActionConstants.Statuses.Deferred;

    private static bool IsOpenOutcomeStatus(string? status)
        => status is AnalyticsActionConstants.OutcomeStatuses.Pending or AnalyticsActionConstants.OutcomeStatuses.NotMeasured;

    private static bool IsStale(string? freshnessStatus)
        => string.Equals(freshnessStatus, "stale", StringComparison.OrdinalIgnoreCase)
            || string.Equals(freshnessStatus, "critical", StringComparison.OrdinalIgnoreCase);

    private static int PriorityRank(string priority) => priority switch
    {
        AnalyticsActionConstants.Priorities.P1 => 1,
        AnalyticsActionConstants.Priorities.P2 => 2,
        _ => 3
    };

    private static bool IsStockWarning(string value)
        => value.Contains("stock", StringComparison.OrdinalIgnoreCase)
            || value.Contains("low", StringComparison.OrdinalIgnoreCase)
            || value.Contains("cover", StringComparison.OrdinalIgnoreCase)
            || value.Contains("sell", StringComparison.OrdinalIgnoreCase);

    private static string BuildSupplierActionSourceKey(
        SummarySupplierItem item,
        DateTime fromDate,
        DateTime toDate,
        int? storeId,
        string dataScope,
        bool recommendationAllowed)
    {
        var actionKind = recommendationAllowed ? "negotiation" : "signal_check";
        return $"supplier:{actionKind}:{item.SupplierId}:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}:{storeId?.ToString(CultureInfo.InvariantCulture) ?? "all"}:{dataScope}";
    }

    private static string ResolveConfidenceLevel(decimal? confidenceScore)
    {
        if (!confidenceScore.HasValue)
        {
            return "insufficient_data";
        }

        return confidenceScore.Value >= 75m ? "high"
            : confidenceScore.Value >= 55m ? "medium"
            : "low";
    }

    private static (string Level, int? Score) ResolveProductConfidence(
        ProductDecisionCenterRowDto row,
        bool recommendationAllowed)
    {
        if (!recommendationAllowed
            || string.Equals(row.ConfidenceLevel, "insufficient_data", StringComparison.OrdinalIgnoreCase))
        {
            // A blocked PDC signal may keep its raw diagnostic percentage, but it is
            // not decision confidence and must not rank or look executable on the board.
            return ("insufficient_data", null);
        }

        var score = row.ConfidenceScore ?? row.ConfidencePct;
        var level = ResolveConfidenceLevel(score);
        return (level, score);
    }

    private static bool IsProductRecommendationAllowed(ProductDecisionCenterRowDto row)
    {
        if (!row.RecommendationAllowed)
        {
            return false;
        }

        var dataQualityStatus = NormalizeDataQualityStatus(row.DataQualityStatus);
        if (dataQualityStatus is "insufficient_data" or "critical" or "error" or "failed")
        {
            return false;
        }

        var freshness = (row.InputFreshnessStatus ?? string.Empty).Trim();
        return !string.Equals(freshness, "stale", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(freshness, "critical", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(freshness, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveProductConfidenceSource(ProductDecisionCenterRowDto row)
    {
        if (string.Equals(row.RecommendationStatus, "FIX_DATA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.RecommendationStatus, "INSUFFICIENT_DATA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.ConfidenceLevel, "insufficient_data", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.DataQualityStatus, "insufficient_data", StringComparison.OrdinalIgnoreCase))
        {
            return "workflow_status_only";
        }

        return "signal";
    }

    private static decimal ComputePriorityScore(
        decimal? expectedImpact,
        int? confidenceScore,
        string? dataQualityStatus,
        string? recommendationStatus)
    {
        var impactComponent = Math.Min(Math.Max(expectedImpact ?? 0m, 0m), 500_000m) / 5_000m;
        var confidenceComponent = Math.Min(Math.Max(confidenceScore ?? 0, 0), 100);
        var dataQualityPenalty =
            string.Equals(dataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase)
                ? 35m
                : string.Equals(dataQualityStatus, "warning", StringComparison.OrdinalIgnoreCase)
                    ? 15m
                    : string.Equals(dataQualityStatus, "insufficient_data", StringComparison.OrdinalIgnoreCase)
                        ? 25m
                        : 0m;
        var statusBonus =
            string.Equals(recommendationStatus, "REPLENISH", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recommendationStatus, "EXPAND", StringComparison.OrdinalIgnoreCase)
                ? 20m
                : string.Equals(recommendationStatus, "BOOST", StringComparison.OrdinalIgnoreCase)
                    ? 18m
                    : string.Equals(recommendationStatus, "MARKDOWN", StringComparison.OrdinalIgnoreCase)
                        ? 14m
                        : string.Equals(recommendationStatus, "FIX_DATA", StringComparison.OrdinalIgnoreCase)
                            ? 22m
                            : string.Equals(recommendationStatus, "INSUFFICIENT_DATA", StringComparison.OrdinalIgnoreCase)
                                ? -15m
                                : 0m;

        return impactComponent + confidenceComponent + statusBonus - dataQualityPenalty;
    }

    private static decimal CapInsufficientDataPriority(decimal priorityScore, string confidenceLevel, string? dataQualityStatus)
    {
        if (string.Equals(confidenceLevel, "insufficient_data", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dataQualityStatus, "insufficient_data", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Min(priorityScore, 40m);
        }

        return priorityScore;
    }

    private static bool IsActionableSupplierDecisionCard(DecisionBoardCardDto card)
    {
        if (!string.Equals(card.Kind, "supplier", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (card.WarningCodes.Contains("supplier_recommendation_blocked", StringComparer.Ordinal))
        {
            return false;
        }

        return !string.Equals(card.ConfidenceLevel, "insufficient_data", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ComputeSupplierPriority(SummarySupplierItem item, ScorecardTrustMetadata? trustMetadata, bool recommendationAllowed)
    {
        var recommendationBias = item.RecommendationCode switch
        {
            "EXPAND" or "EXPAND_SELECTIVELY" => 20m,
            "ASSORTMENT_REDUCE" or "PRICE_NEGOTIATE" => 18m,
            "REVIEW_QUALITY" or "OOS_FALSE_NEGATIVE" => 14m,
            _ => 10m
        };

        var trustPenalty = trustMetadata is null ? 0m : trustMetadata.RecommendationAllowed ? 0m : 20m;
        var confidenceBonus = item.ConfidenceScore;
        var allowedPenalty = recommendationAllowed ? 0m : 10m;
        return (item.Revenue / 5_000m) + confidenceBonus + recommendationBias - trustPenalty - allowedPenalty;
    }

    private static string FmtRsd(decimal? value)
        => value.HasValue ? string.Format(CultureInfo.InvariantCulture, "{0:N2} RSD", value.Value) : "Nije dostupno";

    private static string FmtPct(decimal? value)
        => value.HasValue ? string.Format(CultureInfo.InvariantCulture, "{0:N1}%", value.Value) : "Nije dostupno";

    private static string DeriveWorstStatus(IEnumerable<string?> statuses)
    {
        static int Rank(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            "critical" => 4,
            "stale" => 4,
            "warning" => 3,
            "insufficient_data" => 2,
            "good" or "excellent" => 1,
            "fresh" => 1,
            "unknown" or null or "" => 0,
            _ => 1
        };

        var worst = statuses.OrderByDescending(Rank).FirstOrDefault();
        return string.IsNullOrWhiteSpace(worst) ? "unknown" : worst!;
    }

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        var responseHeader = httpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(responseHeader))
        {
            return responseHeader;
        }

        var requestHeader = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requestHeader))
        {
            return requestHeader;
        }

        return httpContext.TraceIdentifier;
    }

    private enum ActionState
    {
        None,
        Open,
        Closed
    }
}
