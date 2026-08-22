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

    public DecisionPulseService(
        ITrendplusDbContext trendDb,
        IAnalyticsDbContext analyticsDb,
        IAnalyticsCacheService cache,
        IInventoryActionDecisionService inventoryActionDecisionService,
        IEmailService emailService,
        IConfiguration configuration,
        IOptions<DecisionPulseOptions> options)
    {
        _trendDb = trendDb;
        _analyticsDb = analyticsDb;
        _cache = cache;
        _inventoryActionDecisionService = inventoryActionDecisionService;
        _emailService = emailService;
        _configuration = configuration;
        _options = options.Value;
    }

    public async Task<DecisionPulseResponseDto> GetFeedAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int? storeId,
        int? supplierId,
        string? dataScope,
        CancellationToken ct)
    {
        var periodTo = (toUtc ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
        var periodFrom = (fromUtc ?? periodTo.Date.AddDays(-29)).Date;
        var candidates = new List<DecisionPulseCandidate>();
        var sourceFailures = new List<string>();
        var sourceFailureMessages = new List<string>();
        DateTime? generatedAtUtc = null;

        try
        {
            var pdc = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
                _trendDb,
                periodFrom,
                periodTo,
                storeId,
                supplierId,
                top: Math.Clamp(_options.MaxCandidates, 10, 500),
                dataScope ?? string.Empty,
                ct);

            candidates.AddRange((pdc.Rows ?? []).Select(MapProductCandidate));
            generatedAtUtc = MaxGeneratedAt(generatedAtUtc, pdc.GeneratedAtUtc);
        }
        catch (Exception ex)
        {
            sourceFailures.Add("product_source_unavailable");
            sourceFailureMessages.Add("Product Decision izvor nije dostupan.");
            _ = ex;
        }

        try
        {
            var inventoryWorkflow = await InventoryEndpoints.GetInventoryActionWorkflowAsync(
                _cache,
                _trendDb,
                _analyticsDb,
                _inventoryActionDecisionService,
                storeId,
                supplierId,
                search: null,
                ct);

            candidates.AddRange(
                (inventoryWorkflow.Items ?? [])
                    .Select(item => MapInventoryCandidate(item, inventoryWorkflow.GeneratedAtUtc)));
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
                    dataScope,
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
                sourceFailureMessages.Add(validationError is null
                    ? "Supplier filters nisu validni."
                    : "Supplier filters nisu validni.");
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
            return ToResponse(projection, periodFrom, periodTo, generatedAtUtc, sourceFailures, sourceFailureMessages);
        }

        var successProjection = DecisionPulseProjector.Project(candidates, sourceSucceeded: true);
        return ToResponse(successProjection, periodFrom, periodTo, generatedAtUtc, sourceFailures, sourceFailureMessages);
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

    internal static DecisionPulseCandidate MapProductCandidate(ProductDecisionCenterRowDto row)
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
            null);

    internal static DecisionPulseCandidate MapInventoryCandidate(
        InventoryActionSuggestionDto item,
        DateTime generatedAtUtc)
        => new(
            $"inventory:{item.SuggestionKey}",
            DecisionPulseProjector.SourceTypeInventory,
            item.SuggestionKey,
            item.Label,
            item.Reason,
            item.SignalReasonCodes ?? [],
            NormalizeInventoryPulseStatus(item.ActionType),
            item.Label,
            item.SignalDataQualityStatus ?? "good",
            "fresh",
            item.RecommendationAllowed ?? true,
            DecisionPulseProjector.InventoryDeepLink,
            item.UpdatedAtUtc ?? generatedAtUtc);

    internal static IEnumerable<DecisionPulseCandidate> MapSupplierCandidates(SummaryResponse summary)
    {
        var trust = summary.TrustMetadata;
        var generatedAtUtc = trust?.LastRefreshAtUtc ?? DateTime.UtcNow;

        return summary.TopGrowSuppliers
            .Concat(summary.TopRiskSuppliers)
            .Select(item => MapSupplierCandidate(item, trust, generatedAtUtc))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    internal static DecisionPulseCandidate MapSupplierCandidate(
        SummarySupplierItem item,
        ScorecardTrustMetadata? trust,
        DateTime generatedAtUtc)
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
            "fresh",
            trust?.RecommendationAllowed ?? true,
            DecisionPulseProjector.SupplierDeepLink,
            trust?.LastRefreshAtUtc ?? generatedAtUtc);

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
            dto.TenantScope);

    private static DecisionPulseResponseDto ToResponse(
        DecisionPulseProjection projection,
        DateTime? periodFrom,
        DateTime? periodTo,
        DateTime? generatedAtUtc,
        IReadOnlyList<string> sourceFailures,
        IReadOnlyList<string> sourceFailureMessages)
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
            item.DeepLink,
            item.GeneratedAtUtc,
            item.TenantScope)).ToArray();

        DecisionPulseResponseMetaDto meta;
        if (!projection.SourceSucceeded)
        {
            meta = DecisionPulseResponseMetaFactory.Error(
                projection.FailureCategory ?? "source_error",
                projection.FailureMessage ?? "Pulse izvor nije dostupan.",
                correlationId: null);
        }
        else if (items.Length == 0)
        {
            meta = DecisionPulseResponseMetaFactory.Empty(
                "no_pulse_items",
                projection.SuppressedCount > 0
                    ? "Nema actionable Pulse stavki posle potiskivanja stale/empty/insufficient dokaza."
                    : "Nema Decision Pulse izuzetaka za period.",
                "insufficient_data");
        }
        else
        {
            meta = DecisionPulseResponseMetaFactory.Success(
                "good",
                generatedAtUtc,
                isPartial: projection.SuppressedCount > 0 || sourceFailures.Count > 0,
                warningCode: projection.SuppressedCount > 0
                    ? "PULSE_SUPPRESSED"
                    : sourceFailures.Count > 0
                        ? "PULSE_PARTIAL"
                        : null,
                warningMessage: projection.SuppressedCount > 0
                    ? $"Potisnuto {projection.SuppressedCount} stavki zbog stale/empty/insufficient dokaza."
                    : sourceFailures.Count > 0
                        ? $"Neki Decision Pulse izvori nisu dostupni ({string.Join(", ", sourceFailures)})."
                        : null);
        }

        return new DecisionPulseResponseDto(
            generatedAtUtc ?? DateTime.UtcNow,
            periodFrom,
            periodTo,
            projection.TenantScope,
            projection.SuppressedCount,
            items,
            meta);
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
