using Api.Config;
using Application.Analytics.DecisionPulse;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Inventory.Models;
using Infrastructure.Configuration;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.Options;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;

namespace Api.Services.Analytics;

public sealed class DecisionPulseService
{
    private readonly ITrendplusDbContext _trendDb;
    private readonly IAnalyticsDbContext _analyticsDb;
    private readonly IAnalyticsCacheService _cache;
    private readonly IInventoryActionDecisionService _inventoryActionDecisionService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly DecisionPulseOptions _options;
    private readonly Api.Services.AnalyticsRefreshStatusService? _refreshStatusService;

    public DecisionPulseService(
        ITrendplusDbContext trendDb,
        IAnalyticsDbContext analyticsDb,
        IAnalyticsCacheService cache,
        IInventoryActionDecisionService inventoryActionDecisionService,
        IEmailService emailService,
        IConfiguration configuration,
        IOptions<DecisionPulseOptions> options,
        Api.Services.AnalyticsRefreshStatusService? refreshStatusService = null)
    {
        _trendDb = trendDb;
        _analyticsDb = analyticsDb;
        _cache = cache;
        _inventoryActionDecisionService = inventoryActionDecisionService;
        _emailService = emailService;
        _configuration = configuration;
        _options = options.Value;
        _refreshStatusService = refreshStatusService;
    }

    public async Task<DecisionPulseResponseDto> GetFeedAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int? storeId,
        int? supplierId,
        string? dataScope,
        CancellationToken ct)
    {
        var requestedPeriodFromUtc = fromUtc;
        var requestedPeriodToUtc = toUtc;
        // Invalid store/supplier IDs must never widen a narrowed feed to all records.
        if (storeId is <= 0 || supplierId is <= 0)
        {
            var invalidFilter = DecisionPulseProjector.Project(
                null, sourceSucceeded: false,
                failureCategory: "invalid_filter_id",
                failureMessage: "Filter prodavnice ili dobavljača nije validan; pregled nije proširen na sve podatke.");
            var response = ToResponse(
                invalidFilter, null, null, null, [], [], null, null,
                storeId: storeId, supplierId: supplierId,
                requestedPeriodFromUtc: requestedPeriodFromUtc,
                requestedPeriodToUtc: requestedPeriodToUtc,
                requestedDataScope: dataScope);
            response.Meta.EffectiveDataScope = null;
            return response;
        }

        if (!TryNormalizeDataScope(dataScope, out var effectiveDataScope))
        {
            var invalidScope = DecisionPulseProjector.Project(
                null,
                sourceSucceeded: false,
                failureCategory: "unsupported_data_scope",
                failureMessage: "Izabrani opseg podataka nije podržan; pregled nije proširen na druge podatke.");
            var invalidScopeResponse = ToResponse(
                invalidScope, null, null, null, [], [], null, null, storeId: storeId, supplierId: supplierId,
                requestedPeriodFromUtc: requestedPeriodFromUtc, requestedPeriodToUtc: requestedPeriodToUtc);
            invalidScopeResponse.Meta.RequestedDataScope = dataScope;
            invalidScopeResponse.Meta.EffectiveDataScope = null;
            return invalidScopeResponse;
        }

        string? defaultPeriodBasis = null;
        DateTime? resolvedObservedHorizonUtc = null;
        var currentness = "latest_known";
        if (_refreshStatusService is not null)
        {
            try
            {
                var refreshStatus = await _refreshStatusService.GetStatusAsync(ct);
                if (string.Equals(refreshStatus.DataFreshnessStatus, "fresh", StringComparison.OrdinalIgnoreCase))
                    currentness = "current";
            }
            catch
            {
                currentness = "latest_known";
            }
        }
        if (!fromUtc.HasValue && !toUtc.HasValue)
        {
            var defaultPeriod = await ObservedSalesHorizonResolver.ResolveDefaultPeriodAsync(
                _trendDb, storeId, supplierId, effectiveDataScope, ct);
            if (defaultPeriod is null)
            {
                var unavailable = DecisionPulseProjector.Project(
                    null,
                    sourceSucceeded: false,
                    failureCategory: "source_horizon_unavailable",
                    failureMessage: "Nema opaženog poslovnog datuma prodaje za izabrani opseg.");
                return ToResponse(unavailable, null, null, null, [], [], "source_horizon_unavailable", null, currentness,
                    storeId, supplierId, effectiveDataScope, requestedPeriodFromUtc, requestedPeriodToUtc, false,
                    suppressInventoryForRequestedPeriod: false, requestedDataScope: dataScope);
            }

            fromUtc = defaultPeriod.FromUtc;
            toUtc = defaultPeriod.HorizonUtc;
            resolvedObservedHorizonUtc = defaultPeriod.HorizonUtc;
            defaultPeriodBasis = "source_horizon";
        }

        var (periodFrom, periodTo) = NormalizePeriod(fromUtc, toUtc, DateTime.UtcNow);
        var candidates = new List<DecisionPulseCandidate>();
        var sourceFailures = new List<string>();
        var sourceFailureMessages = new List<string>();
        DateTime? generatedAtUtc = null;
        string? comparisonUnavailableReasonCode = null;

        const bool inventoryPeriodNotApplied = true;
        var suppressInventoryForRequestedPeriod = requestedPeriodFromUtc.HasValue || requestedPeriodToUtc.HasValue;
        try
        {
            var pdc = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
                _trendDb,
                periodFrom,
                periodTo,
                storeId,
                supplierId,
                top: Math.Clamp(_options.MaxCandidates, 10, 500),
                effectiveDataScope,
                ct,
                observedHorizonUtc: resolvedObservedHorizonUtc);

            candidates.AddRange((pdc.Rows ?? []).Select(row => MapProductCandidate(
                row,
                pdc.Meta?.ObservedPeriodToUtc ?? resolvedObservedHorizonUtc,
                "product_decision_period")));
            generatedAtUtc = MaxGeneratedAt(generatedAtUtc, pdc.GeneratedAtUtc);
            comparisonUnavailableReasonCode = pdc.Meta?.ComparisonUnavailableReasonCode;
        }
        catch (Exception ex)
        {
            sourceFailures.Add("product_source_unavailable");
            sourceFailureMessages.Add("Product Decision izvor nije dostupan.");
            _ = ex;
        }

        if (!suppressInventoryForRequestedPeriod) try
        {
            var inventoryWorkflow = await InventoryEndpoints.GetInventoryActionWorkflowAsync(
                _cache,
                _trendDb,
                _analyticsDb,
                _inventoryActionDecisionService,
                storeId,
                supplierId,
                search: null,
                ct,
                effectiveDataScope);

            candidates.AddRange(
                (inventoryWorkflow.Items ?? [])
                    .Select(item => MapInventoryCandidate(
                        item,
                        inventoryWorkflow.GeneratedAtUtc,
                        inventoryWorkflow.AsOfUtc,
                        inventoryWorkflow.HorizonBasis ?? "inventory_signal_window")));
            generatedAtUtc = MaxGeneratedAt(generatedAtUtc, inventoryWorkflow.GeneratedAtUtc);
        }
        catch (Exception ex)
        {
            sourceFailures.Add("inventory_source_unavailable");
            sourceFailureMessages.Add("Inventory workflow nije dostupan.");
            _ = ex;
        }

        try
        {
            if (SupplierDecisionHubEndpoints.TryCreateFilters(
                    periodFrom,
                    periodTo,
                    category: null,
                    gender: null,
                    seasonId: null,
                    minRevenue: null,
                    onlyHighConfidence: false,
                    excludeOosBeforeMarkdown: false,
                    supplierId,
                    storeId,
                    effectiveDataScope,
                    out var supplierFilters,
                    out var validationError))
            {
                var analyticsConnectionString = AnalyticsConnectionResolver.Resolve(_configuration);
                var dataset = await SupplierDecisionHubEndpoints.GetSupplierRowsCachedAsync(
                    _cache,
                    analyticsConnectionString,
                    supplierFilters!,
                    ct);
                var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, supplierFilters!);
                candidates.AddRange(MapSupplierCandidates(summary));
                generatedAtUtc = MaxGeneratedAt(generatedAtUtc, summary.TrustMetadata?.LastRefreshAtUtc);
            }
            else
            {
                sourceFailures.Add("supplier_filters_invalid");
                sourceFailureMessages.Add(FormatValidationError(validationError) ?? "Supplier filters nisu validni.");
            }
        }
        catch (Exception ex)
        {
            sourceFailures.Add("supplier_source_unavailable");
            sourceFailureMessages.Add("Supplier decision hub nije dostupan.");
            _ = ex;
        }

        if (candidates.Count == 0 && sourceFailures.Count > 0)
        {
            var projection = DecisionPulseProjector.Project(
                null,
                sourceSucceeded: false,
                failureCategory: sourceFailures[0],
                failureMessage: sourceFailureMessages.FirstOrDefault() ?? "Decision Pulse izvori nisu dostupni.");
            return ToResponse(projection, periodFrom, periodTo, generatedAtUtc, sourceFailures, sourceFailureMessages, defaultPeriodBasis, comparisonUnavailableReasonCode, currentness, storeId, supplierId, effectiveDataScope, requestedPeriodFromUtc, requestedPeriodToUtc, inventoryPeriodNotApplied, suppressInventoryForRequestedPeriod, dataScope);
        }

        var successProjection = DecisionPulseProjector.Project(candidates, sourceSucceeded: true);
        return ToResponse(successProjection, periodFrom, periodTo, generatedAtUtc, sourceFailures, sourceFailureMessages, defaultPeriodBasis, comparisonUnavailableReasonCode, currentness, storeId, supplierId, effectiveDataScope, requestedPeriodFromUtc, requestedPeriodToUtc, inventoryPeriodNotApplied, suppressInventoryForRequestedPeriod, dataScope);
    }

    public Task<DecisionPulseEmailResultDto> SendEmailAsync(
        DecisionPulseResponseDto feed,
        CancellationToken ct)
        => SendEmailAsync(feed, null, ct);

    public async Task<DecisionPulseEmailResultDto> SendEmailAsync(
        DecisionPulseResponseDto feed,
        IReadOnlyList<string>? recipientsOverride,
        CancellationToken ct)
    {
        if (!feed.Meta.Success)
        {
            return new DecisionPulseEmailResultDto(
                false,
                "source_error",
                "Pulse email nije poslat jer je izvor u grešci.",
                0,
                feed.Items.Count);
        }

        var recipients = ResolveRecipients(recipientsOverride);
        if (recipients.Length == 0)
        {
            return new DecisionPulseEmailResultDto(
                false,
                "recipients_missing",
                "Nema konfigurisanih DecisionPulse recipients.",
                0,
                feed.Items.Count);
        }

        if (!_emailService.IsEnabled)
        {
            return new DecisionPulseEmailResultDto(
                false,
                "smtp_disabled",
                "SMTP nije uključen; feed je dostupan in-app.",
                0,
                feed.Items.Count);
        }

        var utcNow = DateTime.UtcNow;
        var message = new EmailMessage
        {
            To = recipients.ToList(),
            Subject = DecisionPulseEmailComposer.BuildSubject(feed.Items.Count, utcNow),
            HtmlBody = DecisionPulseEmailComposer.BuildHtmlBody(feed.Items.Select(MapItem).ToArray(), utcNow)
        };

        await _emailService.SendAsync(message, ct);
        return new DecisionPulseEmailResultDto(
            true,
            null,
            $"Poslato na {recipients.Length} primalaca.",
            recipients.Length,
            feed.Items.Count);
    }

    internal static DecisionPulseCandidate MapProductCandidate(
        ProductDecisionCenterRowDto row,
        DateTime? asOfUtc = null,
        string evidenceBasis = "product_decision_period")
        => new(
            string.IsNullOrWhiteSpace(row.RecommendationId)
                ? $"product:{row.ProductId}"
                : row.RecommendationId,
            DecisionPulseProjector.SourceTypeProduct,
            string.IsNullOrWhiteSpace(row.SourceKey) ? row.ProductId.ToString() : row.SourceKey,
            string.IsNullOrWhiteSpace(row.ProductName) ? row.Sku : $"{row.Sku} — {row.ProductName}",
            FirstNonEmpty(row.RecommendationReason, row.ExplainabilityText, row.RecommendedAction),
            row.ReasonCodes ?? [],
            row.RecommendationStatus,
            row.RecommendationLabel,
            row.DataQualityStatus,
            row.InputFreshnessStatus,
            row.RecommendationAllowed,
            DecisionPulseProjector.ProductDeepLink,
            null,
            asOfUtc,
            evidenceBasis,
            row.RecommendationAllowed ? row.ExpectedImpactRsd : null);

    internal static DecisionPulseCandidate MapInventoryCandidate(
        InventoryActionSuggestionDto item,
        DateTime generatedAtUtc,
        DateTime? asOfUtc = null,
        string evidenceBasis = "inventory_signal_window")
        => new(
            $"inventory:{item.SuggestionKey}",
            DecisionPulseProjector.SourceTypeInventory,
            item.SuggestionKey,
            item.Label,
            item.Reason,
            item.SignalReasonCodes ?? [],
            NormalizeInventoryPulseStatus(item.ActionType),
            item.Label,
            item.SignalDataQualityStatus ?? "insufficient_data",
            "unknown",
            item.RecommendationAllowed ?? false,
            DecisionPulseProjector.InventoryDeepLink,
            item.UpdatedAtUtc ?? generatedAtUtc,
            asOfUtc,
            evidenceBasis,
            null,
            item.Priority);

    internal static IEnumerable<DecisionPulseCandidate> MapSupplierCandidates(SummaryResponse summary)
    {
        var trust = summary.TrustMetadata;
        var generatedAtUtc = trust?.LastRefreshAtUtc;

        return summary.TopGrowSuppliers
            .Concat(summary.TopRiskSuppliers)
            .Select(item => MapSupplierCandidate(item, trust, generatedAtUtc))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    internal static DecisionPulseCandidate MapSupplierCandidate(
        SummarySupplierItem item,
        ScorecardTrustMetadata? trust,
        DateTime? generatedAtUtc)
        => new(
            $"supplier:{item.SupplierId}:{item.RecommendationCode}",
            DecisionPulseProjector.SourceTypeSupplier,
            item.SupplierId.ToString(),
            item.SupplierName,
            item.StatusReason,
            item.ReasonCodes,
            NormalizeSupplierPulseStatus(item.RecommendationCode),
            ResolveSupplierRecommendationLabel(item.RecommendationCode),
            item.DataQualityStatus,
            "unknown",
            trust?.RecommendationAllowed ?? false,
            $"{DecisionPulseProjector.SupplierDeepLink}&supplierId={item.SupplierId}",
            trust?.LastRefreshAtUtc ?? generatedAtUtc,
            trust?.EffectiveTo,
            trust?.ProvenanceBasis ?? "supplier_scorecard_effective_period");

    internal static DecisionPulseItem MapItem(DecisionPulseItemDto dto)
        => new(
            dto.Id,
            dto.SourceType,
            dto.SourceKey,
            dto.Title,
            dto.WhySummary,
            dto.ReasonCodes,
            dto.RecommendationStatus,
            dto.RecommendationLabel,
            dto.DataQualityStatus,
            dto.InputFreshnessStatus,
            dto.DeepLink,
            dto.GeneratedAtUtc,
            dto.TenantScope,
            dto.AsOfUtc,
            dto.EvidenceBasis,
            dto.ExpectedImpactRsd,
            dto.PriorityEvidence);

    internal static (DateTime FromUtc, DateTime ToUtc) NormalizePeriod(
        DateTime? fromUtc,
        DateTime? toUtc,
        DateTime utcNow)
    {
        // Date-only filters use inclusive UTC calendar days; downstream Pulse sources receive these same bounds.
        var to = (toUtc ?? utcNow).Date.AddDays(1).AddTicks(-1);
        var from = (fromUtc ?? to.Date.AddDays(-29)).Date;
        return (from, to);
    }

    internal static bool TryNormalizeDataScope(string? requestedScope, out string effectiveScope)
    {
        if (string.IsNullOrWhiteSpace(requestedScope))
        {
            effectiveScope = "all";
            return true;
        }

        switch (requestedScope.Trim().ToLowerInvariant())
        {
            case "all":
            case "existing":
            case "imported":
                effectiveScope = requestedScope.Trim().ToLowerInvariant();
                return true;
            default:
                effectiveScope = string.Empty;
                return false;
        }
    }

    internal static DecisionPulseResponseDto ToResponse(
        DecisionPulseProjection projection,
        DateTime? periodFrom,
        DateTime? periodTo,
        DateTime? generatedAtUtc,
        IReadOnlyList<string> sourceFailures,
        IReadOnlyList<string> sourceFailureMessages,
        string? defaultPeriodBasis,
        string? comparisonUnavailableReasonCode,
        string currentness = "latest_known",
        int? storeId = null,
        int? supplierId = null,
        string? dataScope = null,
        DateTime? requestedPeriodFromUtc = null,
        DateTime? requestedPeriodToUtc = null,
        bool inventoryPeriodNotApplied = false,
        bool suppressInventoryForRequestedPeriod = false,
        string? requestedDataScope = null)
    {
        var items = projection.Items.Select(item => new DecisionPulseItemDto(
            item.Id,
            item.SourceType,
            item.SourceKey,
            item.Title,
            item.WhySummary,
            item.ReasonCodes,
            item.RecommendationStatus,
            item.RecommendationLabel,
            item.DataQualityStatus,
            item.InputFreshnessStatus,
            AddContext(item.DeepLink, storeId, supplierId, dataScope, periodFrom, periodTo),
            item.GeneratedAtUtc,
            item.TenantScope,
            item.AsOfUtc,
            item.EvidenceBasis,
            item.ExpectedImpactRsd,
            item.PriorityEvidence)).ToArray();

        var meta = BuildResponseMeta(projection, generatedAtUtc, sourceFailures, sourceFailureMessages);
        meta.DefaultPeriodBasis = defaultPeriodBasis;
        meta.RequestedPeriodFromUtc = requestedPeriodFromUtc;
        meta.RequestedPeriodToUtc = requestedPeriodToUtc;
        meta.EffectivePeriodFromUtc = periodFrom;
        meta.EffectivePeriodToUtc = periodTo;
        meta.RequestedDataScope = requestedDataScope ?? dataScope;
        _ = TryNormalizeDataScope(dataScope, out var effectiveDataScope);
        meta.EffectiveDataScope = effectiveDataScope;
        meta.NotAppliedDimensions = inventoryPeriodNotApplied ? ["period:inventory"] : [];
        meta.SuppressedSources = suppressInventoryForRequestedPeriod ? ["inventory"] : [];
        if (inventoryPeriodNotApplied && meta.Success)
        {
            meta.IsPartial = true;
            meta.WarningCode ??= "PULSE_FILTER_NOT_APPLIED";
            meta.WarningMessage ??= suppressInventoryForRequestedPeriod
                ? "Inventarni izvor ne podržava izabrani period i izostavljen je iz pregleda."
                : "Inventarni izvor koristi sopstveni signalni period; izabrani period nije primenjen.";
            meta.Message = string.Join(" ", new[] { meta.Message, meta.WarningMessage }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
        meta.ComparisonUnavailableReasonCode = comparisonUnavailableReasonCode;

        return new DecisionPulseResponseDto(
            generatedAtUtc ?? DateTime.UtcNow,
            periodFrom,
            periodTo,
            projection.TenantScope,
            projection.SuppressedCount,
            items,
            meta,
            currentness,
            projection.Items.Select(item => item.AsOfUtc).Where(value => value.HasValue).Max());
    }

    private static string AddContext(
        string path,
        int? storeId,
        int? supplierId,
        string? dataScope,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var query = new List<string>();
        if (fromDate.HasValue && !path.Contains("fromDate=", StringComparison.OrdinalIgnoreCase)) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue && !path.Contains("toDate=", StringComparison.OrdinalIgnoreCase)) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (storeId.HasValue && !path.Contains("storeId=", StringComparison.OrdinalIgnoreCase)) query.Add($"storeId={storeId.Value}");
        if (supplierId.HasValue && !path.Contains("supplierId=", StringComparison.OrdinalIgnoreCase)) query.Add($"supplierId={supplierId.Value}");
        if (!string.IsNullOrWhiteSpace(dataScope) && !path.Contains("dataScope=", StringComparison.OrdinalIgnoreCase)) query.Add($"dataScope={Uri.EscapeDataString(dataScope)}");
        return query.Count == 0 ? path : $"{path}{(path.Contains('?') ? '&' : '?')}{string.Join('&', query)}";
    }

    private static string? FormatValidationError(Dictionary<string, string[]>? validationError)
    {
        if (validationError is null || validationError.Count == 0)
        {
            return null;
        }

        var parts = validationError
            .Select(pair => pair.Value is { Length: > 0 }
                ? $"{pair.Key}: {string.Join(", ", pair.Value)}"
                : pair.Key)
            .ToArray();

        return parts.Length == 0
            ? null
            : $"Supplier filters nisu validni ({string.Join("; ", parts)}).";
    }

    internal static DecisionPulseResponseMetaDto BuildResponseMeta(
        DecisionPulseProjection projection,
        DateTime? generatedAtUtc,
        IReadOnlyList<string> sourceFailures,
        IReadOnlyList<string> sourceFailureMessages)
    {
        if (!projection.SourceSucceeded)
        {
            return DecisionPulseResponseMetaFactory.Error(
                projection.FailureCategory ?? "source_error",
                projection.FailureMessage ?? "Pulse izvor nije dostupan.",
                correlationId: null);
        }

        if (projection.Items.Count == 0)
        {
            var message = projection.SuppressedCount > 0
                ? $"Nema stavki za prikaz posle izostavljanja {projection.SuppressedCount} kandidata zbog nepouzdanih dokaza ili ograničenja pregleda na 10."
                : "Nema Decision Pulse izuzetaka za period.";

            if (sourceFailures.Count == 0)
            {
                return DecisionPulseResponseMetaFactory.Empty(
                    "no_pulse_items",
                    message,
                    "insufficient_data");
            }

            var sourceFailureMessage = sourceFailureMessages.FirstOrDefault()
                ?? $"Neki Decision Pulse izvori nisu dostupni ({string.Join(", ", sourceFailures)}).";
            var combinedMessage = $"{message} {sourceFailureMessage}";
            var meta = DecisionPulseResponseMetaFactory.Empty(
                "no_pulse_items",
                combinedMessage,
                "insufficient_data");
            meta.IsPartial = true;
            meta.WarningCode = "PULSE_PARTIAL";
            meta.WarningMessage = sourceFailureMessage;
            meta.Message = combinedMessage;
            return meta;
        }

        var dataQualityStatus = projection.Items.Any(item =>
            string.Equals(item.DataQualityStatus, "warning", StringComparison.OrdinalIgnoreCase))
            ? "warning"
            : "good";
        return DecisionPulseResponseMetaFactory.Success(
            dataQualityStatus,
            generatedAtUtc,
            isPartial: projection.SuppressedCount > 0 || sourceFailures.Count > 0,
            warningCode: projection.SuppressedCount > 0
                ? "PULSE_SUPPRESSED"
                : sourceFailures.Count > 0
                    ? "PULSE_PARTIAL"
                    : null,
            warningMessage: projection.SuppressedCount > 0
                ? $"Izostavljeno {projection.SuppressedCount} kandidata zbog nepouzdanih dokaza ili ograničenja pregleda na 10."
                : sourceFailures.Count > 0
                    ? $"Neki Decision Pulse izvori nisu dostupni ({string.Join(", ", sourceFailures)})."
                    : null);
    }

    private string[] ResolveRecipients(IReadOnlyList<string>? recipientsOverride)
    {
        var recipients = (recipientsOverride is { Count: > 0 }
                ? recipientsOverride
                : _options.Recipients ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return recipients;
    }

    private static string NormalizeInventoryPulseStatus(string? actionType)
        => (actionType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "replenish" => "REPLENISH",
            "markdown" => "MARKDOWN",
            "clearance" => "MARKDOWN",
            "transfer" => "TRANSFER",
            "boost" => "BOOST",
            "hold_buy" => "HOLD_BUY",
            "watch" => "WATCH",
            _ => "WATCH"
        };

    private static string NormalizeSupplierPulseStatus(string? recommendationCode)
        => (recommendationCode ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "EXPAND" or "EXPAND_SELECTIVELY" => "BOOST",
            "ASSORTMENT_REDUCE" or "PRICE_NEGOTIATE" => "MARKDOWN",
            "REVIEW_QUALITY" or "OOS_FALSE_NEGATIVE" => "WATCH",
            "HOLD" => "HOLD_BUY",
            "HOLD_BUY" => "HOLD_BUY",
            "REPLENISH" => "REPLENISH",
            "MARKDOWN" => "MARKDOWN",
            "TRANSFER" => "TRANSFER",
            "BOOST" => "BOOST",
            "WATCH" => "WATCH",
            _ => "WATCH"
        };

    private static string ResolveSupplierRecommendationLabel(string? recommendationCode)
        => (recommendationCode ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "EXPAND" => "Širi saradnju",
            "EXPAND_SELECTIVELY" => "Selektivno širi",
            "ASSORTMENT_REDUCE" => "Smanji asortiman",
            "PRICE_NEGOTIATE" => "Pregovaraj cenu",
            "REVIEW_QUALITY" => "Proveri kvalitet",
            "OOS_FALSE_NEGATIVE" => "Proveri OOS signal",
            "HOLD" => "Zadrži",
            "HOLD_BUY" => "Zadrži kupovinu",
            "REPLENISH" => "Dopuni",
            "MARKDOWN" => "Snizi cenu",
            "TRANSFER" => "Transfer",
            "BOOST" => "Pojačaj",
            "WATCH" => "Prati",
            _ => recommendationCode?.Trim() ?? string.Empty
        };

    private static DateTime? MaxGeneratedAt(DateTime? current, DateTime? candidate)
    {
        if (!candidate.HasValue)
        {
            return current;
        }

        if (!current.HasValue || candidate.Value > current.Value)
        {
            return candidate;
        }

        return current;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
