using System.Globalization;
using Api.Config;
using Application.Analytics;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendplus2.Dtos;

namespace Api.Services;

/// <summary>
/// Binds the six current Operations surfaces to scoped sales facts and durable
/// import evidence. Import batches are global, so they cannot certify filtered
/// stores, existing-origin rows, or mixed-origin populations.
/// </summary>
public sealed class OperationsSourceFreshnessService
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TrendplusDbContext _db;
    private readonly ILogger<OperationsSourceFreshnessService> _logger;
    private readonly AnalyticsFreshnessOptions _freshnessOptions;

    public OperationsSourceFreshnessService(
        TrendplusDbContext db,
        ILogger<OperationsSourceFreshnessService> logger,
        IOptions<AnalyticsFreshnessOptions>? freshnessOptions = null)
    {
        _db = db;
        _logger = logger;
        _freshnessOptions = freshnessOptions?.Value ?? new AnalyticsFreshnessOptions();
    }

    public async Task ApplyAsync(
        AnalyticsResponseMetaDto meta,
        string? dataScope,
        int? storeId,
        string surfaceRole,
        CancellationToken ct = default,
        string? defaultPeriodBasis = null,
        string? comparisonUnavailableReasonCode = null)
    {
        meta.DefaultPeriodBasis = defaultPeriodBasis;
        meta.ComparisonUnavailableReasonCode = comparisonUnavailableReasonCode;
        ResetSourceFreshness(meta);
        try
        {
            var scope = SalesDataScopePolicy.Normalize(dataScope);
            var source = (meta.DataScopeSource ?? SalesDataScopePolicy.Source).Trim().ToLowerInvariant();
            var headers = BuildSourceHeaders(
                scope,
                source,
                storeId,
                meta.RequestedPeriodFromUtc,
                meta.RequestedPeriodToUtc);
            meta.ObservedPeriodFromUtc = await headers
                .Select(header => (DateTime?)header.DatumProdaje)
                .MinAsync(ct);
            meta.ObservedPeriodToUtc = await headers
                .Select(header => (DateTime?)header.DatumProdaje)
                .MaxAsync(ct);

            var reason = await ResolveUnwatermarkedScopeReasonAsync(headers, scope, source, storeId, ct);
            if (reason is not null)
            {
                meta.DataFreshnessReasonCode = reason;
                ApplyDecisionReadiness(meta, surfaceRole);
                return;
            }

            var accessBatches = _db.DataImportBatches.AsNoTracking()
                .Where(batch => batch.SourceSystem.ToLower() == "access" && batch.IncludeAnalytics);
            var lastSuccess = await accessBatches
                .Where(batch => batch.Status.ToLower() == "completed"
                    && batch.CompletedAtUtc.HasValue
                    && batch.TotalErrors == 0
                    && batch.RowsRejected == 0)
                .OrderByDescending(batch => batch.CompletedAtUtc)
                .ThenByDescending(batch => batch.Id)
                .Select(batch => new ImportEvidence(batch.Id, batch.CompletedAtUtc, batch.Status))
                .FirstOrDefaultAsync(ct);
            var lastFailure = await accessBatches
                .Where(batch => batch.Status.ToLower() == "failed"
                    || batch.Status.ToLower() == "interrupted"
                    || (batch.Status.ToLower() == "completed" && (batch.TotalErrors > 0 || batch.RowsRejected > 0)))
                .OrderByDescending(batch => batch.CompletedAtUtc ?? batch.StartedAtUtc)
                .ThenByDescending(batch => batch.Id)
                .Select(batch => (DateTime?)(batch.CompletedAtUtc ?? batch.StartedAtUtc))
                .FirstOrDefaultAsync(ct);

            if (lastSuccess is null || !lastSuccess.CompletedAtUtc.HasValue)
            {
                meta.DataFreshnessReasonCode = "source_import_evidence_missing";
                ApplyDecisionReadiness(meta, surfaceRole);
                return;
            }

            var freshness = AnalyticsFreshnessPolicy.Resolve(
                lastSuccess.CompletedAtUtc,
                lastFailure,
                DateTime.UtcNow,
                _freshnessOptions);
            meta.DataFreshnessStatus = freshness;
            meta.DataFreshnessEvidenceId = $"access-import-batch:{lastSuccess.Id.ToString(CultureInfo.InvariantCulture)}";
            meta.DataFreshnessEvidenceAtUtc = lastSuccess.CompletedAtUtc;
            meta.DataFreshnessSourceGeneration = $"access-import:{lastSuccess.Id.ToString(CultureInfo.InvariantCulture)}:{lastSuccess.CompletedAtUtc.Value.ToUniversalTime():O}";
            meta.LastRefreshAtUtc = lastSuccess.CompletedAtUtc;
            meta.DataFreshnessReasonCode = freshness switch
            {
                "fresh" => lastFailure > lastSuccess.CompletedAtUtc ? "source_import_failure_after_success" : "source_import_recent_success",
                "stale" => "source_import_older_than_24h",
                "critical" when lastFailure > lastSuccess.CompletedAtUtc => "source_import_failure_after_success",
                "critical" => "source_import_older_than_72h",
                _ => "source_import_evidence_missing"
            };

            if (meta.OperationsIntegrityFamily is { Length: > 0 } family
                && meta.RequestedPeriodFromUtc.HasValue
                && meta.RequestedPeriodToUtc.HasValue)
            {
                meta.DataFreshnessContextFingerprint = OperationsAnalyticsIntegrityContextPolicy.CreateFingerprint(
                    family,
                    meta.DataFreshnessSourceGeneration,
                    meta.RequestedPeriodFromUtc.Value,
                    meta.RequestedPeriodToUtc.Value,
                    scope,
                    storeId);
            }
            else
            {
                meta.DataFreshnessStatus = "unknown";
                meta.DataFreshnessReasonCode = "source_freshness_context_unavailable";
                meta.DataFreshnessEvidenceId = null;
                meta.DataFreshnessEvidenceAtUtc = null;
                meta.DataFreshnessSourceGeneration = null;
                meta.DataFreshnessContextFingerprint = null;
                meta.LastRefreshAtUtc = null;
            }

            ApplyDecisionReadiness(meta, surfaceRole);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operations source freshness evidence is unavailable; returning unknown freshness.");
            ResetSourceFreshness(meta);
            meta.DataFreshnessReasonCode = "source_freshness_evidence_unavailable";
            ApplyDecisionReadiness(meta, surfaceRole);
        }
    }

    public async Task<string> ApplyJsonAsync(
        string jsonPayload,
        string? dataScope,
        int? storeId,
        string surfaceRole,
        CancellationToken ct = default,
        string? defaultPeriodBasis = null,
        string? comparisonUnavailableReasonCode = null)
    {
        var root = JsonNode.Parse(jsonPayload)?.AsObject()
            ?? throw new InvalidOperationException("Operations response JSON is not an object.");
        if (root["meta"] is not JsonObject metaNode)
            throw new InvalidOperationException("Operations response JSON has no analytics meta object.");

        var meta = metaNode.Deserialize<AnalyticsResponseMetaDto>(WebJsonOptions)
            ?? throw new InvalidOperationException("Operations response analytics meta could not be read.");
        await ApplyAsync(meta, dataScope, storeId, surfaceRole, ct, defaultPeriodBasis, comparisonUnavailableReasonCode);
        root["meta"] = JsonSerializer.SerializeToNode(meta, WebJsonOptions);
        return root.ToJsonString(WebJsonOptions);
    }

    public static async Task ApplyIfRegisteredAsync(
        IServiceProvider services,
        AnalyticsResponseMetaDto meta,
        string? dataScope,
        int? storeId,
        string surfaceRole,
        CancellationToken ct = default,
        string? defaultPeriodBasis = null,
        string? comparisonUnavailableReasonCode = null)
    {
        var service = services.GetService<OperationsSourceFreshnessService>();
        if (service is null)
        {
            MarkUnavailable(meta, surfaceRole);
            return;
        }

        await service.ApplyAsync(meta, dataScope, storeId, surfaceRole, ct, defaultPeriodBasis, comparisonUnavailableReasonCode);
    }

    public static async Task<string> ApplyJsonIfRegisteredAsync(
        IServiceProvider services,
        string jsonPayload,
        string? dataScope,
        int? storeId,
        string surfaceRole,
        CancellationToken ct = default,
        string? defaultPeriodBasis = null,
        string? comparisonUnavailableReasonCode = null)
    {
        var service = services.GetService<OperationsSourceFreshnessService>();
        if (service is not null)
            return await service.ApplyJsonAsync(jsonPayload, dataScope, storeId, surfaceRole, ct, defaultPeriodBasis, comparisonUnavailableReasonCode);

        var root = JsonNode.Parse(jsonPayload)?.AsObject()
            ?? throw new InvalidOperationException("Operations response JSON is not an object.");
        if (root["meta"] is not JsonObject metaNode)
            throw new InvalidOperationException("Operations response JSON has no analytics meta object.");

        var meta = metaNode.Deserialize<AnalyticsResponseMetaDto>(WebJsonOptions)
            ?? throw new InvalidOperationException("Operations response analytics meta could not be read.");
        MarkUnavailable(meta, surfaceRole);
        meta.DefaultPeriodBasis = defaultPeriodBasis;
        meta.ComparisonUnavailableReasonCode = comparisonUnavailableReasonCode;
        root["meta"] = JsonSerializer.SerializeToNode(meta, WebJsonOptions);
        return root.ToJsonString(WebJsonOptions);
    }

    private IQueryable<ProdajaZaglavlje> BuildSourceHeaders(
        string scope,
        string scopeSource,
        int? storeId,
        DateTime? requestedFromUtc,
        DateTime? requestedToUtc)
    {
        var query = _db.ProdajaZaglavlja.AsNoTracking()
            .Where(SalesReceiptPopulationPolicy.IncludedHeaderPredicate);
        var headerScoped = scopeSource.Contains("sale_header", StringComparison.Ordinal)
            || scopeSource.Contains("article-and-sale-header", StringComparison.Ordinal);
        var articleScoped = scopeSource.Contains("product_origin", StringComparison.Ordinal)
            || scopeSource.Contains("article-and-sale-header", StringComparison.Ordinal);

        if (headerScoped && scope != "all")
            query = query.Where(SalesDataScopePolicy.HeaderPredicate(scope));
        if (storeId.HasValue)
            query = query.Where(header => header.IDObjekat == storeId.Value);
        if (requestedFromUtc.HasValue)
            query = query.Where(header => header.DatumProdaje >= requestedFromUtc.Value);
        if (requestedToUtc.HasValue)
            query = query.Where(header => header.DatumProdaje <= requestedToUtc.Value);
        if (articleScoped && scope != "all")
        {
            query = scope switch
            {
                "imported" => query.Where(header => _db.ProdajaStavke.Any(line =>
                    line.IdProdaja == header.Id && _db.Artikli.Any(article => article.Id == line.IdArtikal && article.DataOrigin == "access"))),
                "existing" => query.Where(header => _db.ProdajaStavke.Any(line =>
                    line.IdProdaja == header.Id && _db.Artikli.Any(article => article.Id == line.IdArtikal
                        && (article.DataOrigin == "existing" || article.DataOrigin == null || article.DataOrigin == "")))),
                _ => query
            };
        }

        return query.Where(header => _db.ProdajaStavke.Any(line => line.IdProdaja == header.Id));
    }

    private async Task<string?> ResolveUnwatermarkedScopeReasonAsync(
        IQueryable<ProdajaZaglavlje> headers,
        string scope,
        string scopeSource,
        int? storeId,
        CancellationToken ct)
    {
        if (storeId.HasValue)
            return "source_import_not_store_scoped";
        if (scope == "existing")
            return "source_watermark_unavailable_for_existing_data";

        if (scope == "all")
        {
            var headerScoped = scopeSource.Contains("sale_header", StringComparison.Ordinal)
                || scopeSource.Contains("article-and-sale-header", StringComparison.Ordinal);
            var articleScoped = scopeSource.Contains("product_origin", StringComparison.Ordinal)
                || scopeSource.Contains("article-and-sale-header", StringComparison.Ordinal);
            if (headerScoped && await headers.AnyAsync(header => header.DataOrigin != "access", ct))
                return "source_scope_contains_unwatermarked_rows";
            if (articleScoped && await headers.AnyAsync(header => _db.ProdajaStavke.Any(line =>
                    line.IdProdaja == header.Id && _db.Artikli.Any(article => article.Id == line.IdArtikal
                        && article.DataOrigin != "access")), ct))
                return "source_scope_contains_unwatermarked_rows";
        }

        return null;
    }

    private static void ResetSourceFreshness(AnalyticsResponseMetaDto meta)
    {
        meta.DataFreshnessStatus = "unknown";
        meta.DataFreshnessReasonCode = null;
        meta.DataFreshnessEvidenceId = null;
        meta.DataFreshnessEvidenceAtUtc = null;
        meta.DataFreshnessSourceGeneration = null;
        meta.DataFreshnessContextFingerprint = null;
        meta.LastRefreshAtUtc = null;
    }

    private static void MarkUnavailable(AnalyticsResponseMetaDto meta, string surfaceRole)
    {
        ResetSourceFreshness(meta);
        meta.DataFreshnessReasonCode = "source_freshness_service_unavailable";
        ApplyDecisionReadiness(meta, surfaceRole);
    }

    private static void ApplyDecisionReadiness(AnalyticsResponseMetaDto meta, string surfaceRole)
    {
        var previous = meta.DecisionReadiness;
        var reasons = previous?.ReasonCodes.ToList() ?? [];
        if (meta.DataFreshnessStatus is "unknown" or "stale" or "critical")
        {
            var reason = $"source_freshness.{meta.DataFreshnessReasonCode ?? "unknown"}";
            if (!reasons.Contains(reason, StringComparer.Ordinal))
                reasons.Add(reason);
        }

        var evidence = previous?.EvidenceReferences.ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(meta.DataFreshnessEvidenceId)
            && !evidence.Contains(meta.DataFreshnessEvidenceId, StringComparer.Ordinal))
            evidence.Add(meta.DataFreshnessEvidenceId);

        AnalyticsResponseMetaFactory.ApplyDecisionReadiness(
            meta,
            previous?.SurfaceRole ?? surfaceRole,
            meta.DataFreshnessStatus,
            reasons,
            evidence,
            previous?.RepairPath);
    }

    private sealed record ImportEvidence(long Id, DateTime? CompletedAtUtc, string Status);
}
