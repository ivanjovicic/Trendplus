using Application.Analytics.DecisionPulse;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Inventory.Models;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;

namespace Api.Services.Analytics;

public sealed class DecisionPulseService
{
    private readonly ITrendplusDbContext _trendDb;
    private readonly IEmailService _emailService;
    private readonly DecisionPulseOptions _options;

    public DecisionPulseService(
        ITrendplusDbContext trendDb,
        IEmailService emailService,
        IOptions<DecisionPulseOptions> options)
    {
        _trendDb = trendDb;
        _emailService = emailService;
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
        try
        {
            var periodTo = (toUtc ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
            var periodFrom = (fromUtc ?? periodTo.Date.AddDays(-29)).Date;

            var pdc = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
                _trendDb,
                periodFrom,
                periodTo,
                storeId,
                supplierId,
                top: Math.Clamp(_options.MaxCandidates, 10, 500),
                dataScope ?? string.Empty,
                ct);

            var candidates = (pdc.Rows ?? [])
                .Select(MapCandidate)
                .ToArray();

            var projection = DecisionPulseProjector.Project(candidates, sourceSucceeded: true);
            return ToResponse(projection, periodFrom, periodTo, pdc.GeneratedAtUtc);
        }
        catch (Exception ex)
        {
            var projection = DecisionPulseProjector.Project(
                null,
                sourceSucceeded: false,
                failureCategory: "source_error",
                failureMessage: "Product Decision izvor nije dostupan; Pulse ne izmišlja alert.");
            _ = ex;
            return ToResponse(projection, fromUtc, toUtc, null);
        }
    }

    public async Task<DecisionPulseEmailResultDto> SendEmailAsync(
        DecisionPulseResponseDto feed,
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

        var recipients = (_options.Recipients ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipients.Length == 0)
        {
            return new DecisionPulseEmailResultDto(
                false,
                "recipients_missing",
                "Nema konfigurisanih DecisionPulse:Recipients.",
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

    private static DecisionPulseCandidate MapCandidate(ProductDecisionCenterRowDto row)
        => new(
            string.IsNullOrWhiteSpace(row.RecommendationId)
                ? $"product:{row.ProductId}"
                : row.RecommendationId,
            "product",
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

    private static DecisionPulseItem MapItem(DecisionPulseItemDto dto)
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
        DateTime? generatedAtUtc)
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

        AnalyticsResponseMetaDto meta;
        if (!projection.SourceSucceeded)
        {
            meta = AnalyticsResponseMetaFactory.Error(
                projection.FailureCategory ?? "source_error",
                projection.FailureMessage ?? "Pulse izvor nije dostupan.",
                correlationId: null);
        }
        else if (items.Length == 0)
        {
            meta = AnalyticsResponseMetaFactory.Empty(
                "no_pulse_items",
                projection.SuppressedCount > 0
                    ? "Nema actionable Pulse stavki posle potiskivanja stale/empty/insufficient dokaza."
                    : "Nema Decision Pulse izuzetaka za period.",
                "insufficient_data");
        }
        else
        {
            meta = AnalyticsResponseMetaFactory.Success(
                "good",
                generatedAtUtc,
                isPartial: projection.SuppressedCount > 0,
                warningCode: projection.SuppressedCount > 0 ? "PULSE_SUPPRESSED" : null,
                warningMessage: projection.SuppressedCount > 0
                    ? $"Potisnuto {projection.SuppressedCount} stavki zbog stale/empty/insufficient dokaza."
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

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

public sealed class DecisionPulseOptions
{
    public const string Section = "DecisionPulse";

    public string[] Recipients { get; set; } = [];
    public int MaxCandidates { get; set; } = 100;
}

public sealed record DecisionPulseItemDto(
    string Id,
    string SourceType,
    string SourceKey,
    string Title,
    string WhySummary,
    IReadOnlyList<string> ReasonCodes,
    string RecommendationStatus,
    string RecommendationLabel,
    string DataQualityStatus,
    string InputFreshnessStatus,
    string DeepLink,
    DateTime? GeneratedAtUtc,
    string TenantScope);

public sealed record DecisionPulseResponseDto(
    DateTime GeneratedAtUtc,
    DateTime? PeriodFromUtc,
    DateTime? PeriodToUtc,
    string TenantScope,
    int SuppressedCount,
    IReadOnlyList<DecisionPulseItemDto> Items,
    AnalyticsResponseMetaDto Meta);

public sealed record DecisionPulseEmailResultDto(
    bool Sent,
    string? FailureCategory,
    string Message,
    int RecipientCount,
    int ItemCount);
