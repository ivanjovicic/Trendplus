using System.Globalization;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Documents.Interfaces;
using Application.Inventory.Models;
using Application.Documents.Models;
using Domain.Model;
using Infrastructure.Configuration;
using Infrastructure.Services.Caching;
using Infrastructure.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Trendplus2.Dtos;

namespace Trendplus2.Endpoints;

public static class InventoryEndpoints
{
    private static readonly CultureInfo SerbianCulture = CultureInfo.GetCultureInfo("sr-RS");
    private const int MovementStatsBatchSize = 5_000;

    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/inventory")
            .WithTags("Analytics");

        group.MapGet("/balance", async (
            ITrendplusDbContext db,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            int? storeId,
            int? supplierId,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("InventoryEndpoints");
            var correlationId = AllEndpoints.ResolveAnalyticsCorrelationId(httpContext);

            try
            {
                var query = ApplyInventoryFilters(db.Artikli.AsNoTracking(), storeId, supplierId, null);

                var totalSku = await query.CountAsync(ct);
                var totalOnHand = await query.SumAsync(a => (int?)((a.Kolicina ?? 0) > 0 ? (a.Kolicina ?? 0) : 0), ct) ?? 0;
                var lowStock = await query.CountAsync(a => (a.Kolicina ?? 0) <= (a.MinimalnaKolicina ?? 0) && (a.Kolicina ?? 0) > 0, ct);
                var outOfStock = await query.CountAsync(a => (a.Kolicina ?? 0) <= 0, ct);
                var estimatedValue = await query.SumAsync(a => (decimal?)((a.NabavnaCena ?? 0m) * ((a.Kolicina ?? 0) > 0 ? (a.Kolicina ?? 0) : 0)), ct) ?? 0m;

                var meta = totalSku == 0
                    ? AnalyticsResponseMetaFactory.Empty("no_inventory_data", "Nema podataka o zalihama.")
                    : AnalyticsResponseMetaFactory.Success();
                meta.CorrelationId = correlationId;

                return Results.Ok(new InventoryBalanceDto(
                    TotalSku: totalSku,
                    TotalOnHand: totalOnHand,
                    LowStockCount: lowStock,
                    OutOfStockCount: outOfStock,
                    EstimatedInventoryValue: Math.Round(estimatedValue, 2),
                    Meta: meta));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Inventory balance query failed.");
                return Results.Ok(new InventoryBalanceDto(
                    TotalSku: 0,
                    TotalOnHand: 0,
                    LowStockCount: 0,
                    OutOfStockCount: 0,
                    EstimatedInventoryValue: 0m,
                    Meta: AnalyticsResponseMetaFactory.Error(
                        "inventory_balance_error",
                        "Bilans zaliha trenutno nije dostupan.",
                        correlationId)));
            }
        })
        .WithName("GetInventoryBalance");

        group.MapGet("/list", async (
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            int page = 1,
            int pageSize = 50,
            int? storeId = null,
            int? supplierId = null,
            string? search = null,
            string? sortBy = null,
            string? dataScope = null,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 1000);

            var query = ApplyInventorySorting(
                ApplyInventoryFilters(db.Artikli.AsNoTracking(), storeId, supplierId, search, dataScope: dataScope),
                sortBy);

            var total = await query.CountAsync(ct);
            var rawItems = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.PLU,
                    Naziv = a.Naziv ?? string.Empty,
                    a.Kolicina,
                    a.MinimalnaKolicina,
                    a.NabavnaCena,
                    a.IDObjekat,
                    a.IDDobavljac
                })
                .ToListAsync(ct);

            var articleIds = rawItems.Select(item => item.Id).ToArray();
            var soldUnitsByArticle = await LoadSoldUnitsByArticleAsync(db, articleIds, storeId, 30, ct);
            var movementWindowStatsByArticle = await LoadInventorySignalWindowStatsAsync(analyticsDb, articleIds, storeId, 30, ct);

            var items = new List<InventoryListItemDto>(rawItems.Count);
            foreach (var item in rawItems)
            {
                var quantity = item.Kolicina ?? 0;
                var soldUnits30d = soldUnitsByArticle.TryGetValue(item.Id, out var units) ? units : 0;
                var movementWindowStats = movementWindowStatsByArticle.TryGetValue(item.Id, out var stats)
                    ? stats
                    : new InventorySignalWindowStats(0, 0);
                var openingStockUnits = Math.Max(quantity - movementWindowStats.NetMovementUnits, 0);
                var hasReliableSellThroughInputs = openingStockUnits > 0 || movementWindowStats.InboundUnits > 0;
                var avgDailySalesUnits = Math.Round(soldUnits30d / 30m, 4, MidpointRounding.AwayFromZero);
                var hasSufficientData = soldUnits30d > 0 || quantity > 0 || hasReliableSellThroughInputs;
                var signalDataQuality = soldUnits30d > 0 && hasReliableSellThroughInputs
                    ? "good"
                    : hasSufficientData
                        ? "warning"
                        : "insufficient_data";
                var signal = ComputeInventorySignalEvidence(
                    quantity,
                    avgDailySalesUnits,
                    soldUnits30d,
                    openingStockUnits,
                    movementWindowStats.InboundUnits,
                    signalDataQuality,
                    hasSufficientData);

                items.Add(new InventoryListItemDto(
                    item.Id,
                    item.PLU,
                    item.Naziv,
                    item.Kolicina,
                    item.MinimalnaKolicina,
                    item.NabavnaCena,
                    (item.NabavnaCena ?? 0m) * (quantity > 0 ? quantity : 0),
                    item.IDObjekat,
                    item.IDDobavljac,
                    signal.StockCoverDays,
                    signal.StockCoverStatus,
                    signal.StockCoverStatusLabel,
                    signal.SellThroughRatio,
                    signal.SellThroughStatus,
                    signal.SellThroughStatusLabel,
                    signal.SignalConfidencePct,
                    signal.RecommendationAllowed,
                    signal.ReasonCodes.ToList(),
                    signal.DataQualityStatus));
            }

            var meta = total == 0
                ? AnalyticsResponseMetaFactory.Empty("no_inventory_items", "Nema artikala koji odgovaraju filterima.")
                : AnalyticsResponseMetaFactory.Success();

            return Results.Ok(new ArtikliPagedResponse<InventoryListItemDto>(items, total, page, pageSize, meta));
        })
        .WithName("GetInventoryList");

        group.MapGet("/insights", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            int? storeId = null,
            int? supplierId = null,
            string? search = null,
            string? sortBy = null,
            string? dataScope = null,
            CancellationToken ct = default) =>
        {
            return Results.Ok(await GetInventoryInsightsAsync(cache, db, analyticsDb, storeId, supplierId, search, sortBy, ct, dataScope));
        })
        .WithName("GetInventoryInsights");

        group.MapGet("/{id:int}/detail", async (
            int id,
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            CancellationToken ct) =>
        {
            var article = await db.Artikli
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new InventoryArticleProjection(
                    a.Id,
                    a.PLU,
                    a.Naziv,
                    a.Kolicina ?? 0,
                    a.MinimalnaKolicina ?? 0,
                    a.NabavnaCena ?? 0m,
                    a.IDObjekat,
                    a.IDDobavljac,
                    a.Kategorija,
                    a.Pol,
                    a.Materijal,
                    a.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            if (article is null)
            {
                return Results.NotFound(new { message = $"Artikal sa Id={id} nije pronadjen." });
            }

            var history = await analyticsDb.InventoryMovementFacts
                .AsNoTracking()
                .Where(x => x.ArtikalId == id)
                .OrderByDescending(x => x.Datum)
                .Take(12)
                .Select(x => new InventoryHistoryProjection(
                    x.SourceId,
                    x.TipPromene,
                    x.Datum,
                    x.Kolicina,
                    x.Iznos,
                    x.BrojDokumenta,
                    x.KorisnikIme,
                    x.DataOrigin,
                    x.StoreId,
                    x.DobavljacId,
                    x.StaraProdajnaCena,
                    x.NovaProdajnaCena,
                    null))
                .ToListAsync(ct);

            if (history.Count == 0)
            {
                history = await db.DnevnikPromena
                    .AsNoTracking()
                    .Where(x => x.ArtikalId == id)
                    .OrderByDescending(x => x.Datum)
                    .Take(12)
                    .Select(x => new InventoryHistoryProjection(
                        x.Id,
                        x.TipPromene,
                        x.Datum,
                        x.Kolicina,
                        x.Iznos,
                        x.BrojRacuna,
                        x.KorisnikIme,
                        x.DataOrigin,
                        x.IDObjekat,
                        x.DobavljacId,
                        x.StaraProdajnaCena,
                        x.NovaProdajnaCena,
                        x.Komentar))
                    .ToListAsync(ct);
            }

            var movementCount30d = await analyticsDb.InventoryMovementFacts
                .AsNoTracking()
                .Where(x => x.ArtikalId == id && x.Datum >= DateTime.UtcNow.AddDays(-30))
                .CountAsync(ct);

            if (movementCount30d == 0)
            {
                movementCount30d = await db.DnevnikPromena
                    .AsNoTracking()
                    .Where(x => x.ArtikalId == id && x.Datum >= DateTime.UtcNow.AddDays(-30))
                    .CountAsync(ct);
            }

            var storeNameMap = await LoadStoreNamesAsync(
                analyticsDb,
                history.Select(x => x.StoreId).Append(article.StoreId),
                ct);
            var supplierNameMap = await LoadSupplierNamesAsync(
                analyticsDb,
                db,
                history.Select(x => x.SupplierId).Append(article.SupplierId),
                ct);

            DateTime? lastMovementAt = history.Count > 0
                ? history.Max(x => DateTime.SpecifyKind(x.Datum, DateTimeKind.Utc))
                : null;

            var daysSinceMovement = ResolveDaysSinceMovement(lastMovementAt, article.UpdatedAt);
            var (agingBucket, agingLabel) = ResolveAging(daysSinceMovement);

            var singleton = ApplyAbcClassification(new List<InventoryDatasetItem>
            {
                new(
                    article.Id,
                    article.Plu,
                    article.Naziv,
                    article.Quantity,
                    article.Minimum,
                    article.UnitCost,
                    article.UnitCost * article.Quantity,
                    article.StoreId,
                    ResolveLookup(storeNameMap, article.StoreId),
                    article.SupplierId,
                    ResolveLookup(supplierNameMap, article.SupplierId),
                    article.Kategorija,
                    article.Pol,
                    article.Materijal,
                    EnsureUtc(article.UpdatedAt),
                    lastMovementAt,
                    movementCount30d,
                    daysSinceMovement,
                    agingBucket,
                    agingLabel,
                    "C")
            })
            .Single();

            var articleIds = new[] { singleton.Id };
            var soldUnitsByArticle = await LoadSoldUnitsByArticleAsync(db, articleIds, singleton.StoreId, 30, ct);
            var movementWindowStatsByArticle = await LoadInventorySignalWindowStatsAsync(analyticsDb, articleIds, singleton.StoreId, 30, ct);
            var soldUnits30d = soldUnitsByArticle.TryGetValue(singleton.Id, out var soldUnitsValue) ? soldUnitsValue : 0;
            var movementWindowStats = movementWindowStatsByArticle.TryGetValue(singleton.Id, out var movementStats)
                ? movementStats
                : new InventorySignalWindowStats(0, 0);
            var openingStockUnits = Math.Max(singleton.Quantity - movementWindowStats.NetMovementUnits, 0);
            var hasReliableSellThroughInputs = openingStockUnits > 0 || movementWindowStats.InboundUnits > 0;
            var avgDailySalesUnits = Math.Round(soldUnits30d / 30m, 4, MidpointRounding.AwayFromZero);
            var hasSufficientData = soldUnits30d > 0 || singleton.Quantity > 0 || hasReliableSellThroughInputs;
            var signalDataQuality = soldUnits30d > 0 && hasReliableSellThroughInputs
                ? "good"
                : hasSufficientData
                    ? "warning"
                    : "insufficient_data";
            var signalEvidence = ComputeInventorySignalEvidence(
                singleton.Quantity,
                avgDailySalesUnits,
                soldUnits30d,
                openingStockUnits,
                movementWindowStats.InboundUnits,
                signalDataQuality,
                hasSufficientData);

            var detail = new InventoryItemDetailDto(
                singleton.Id,
                singleton.Plu,
                singleton.Naziv,
                singleton.Quantity,
                singleton.Minimum,
                singleton.UnitCost,
                singleton.EstimatedValue,
                singleton.StoreId,
                singleton.StoreName,
                singleton.SupplierId,
                singleton.SupplierName,
                singleton.Kategorija,
                singleton.Pol,
                singleton.Materijal,
                singleton.UpdatedAtUtc,
                singleton.LastMovementAtUtc,
                singleton.MovementCount30d,
                singleton.DaysSinceMovement,
                singleton.AgingBucket,
                singleton.AgingLabel,
                singleton.AbcClass,
                signalEvidence.StockCoverDays,
                signalEvidence.StockCoverStatus,
                signalEvidence.StockCoverStatusLabel,
                signalEvidence.SellThroughRatio,
                signalEvidence.SellThroughStatus,
                signalEvidence.SellThroughStatusLabel,
                signalEvidence.SignalConfidencePct,
                signalEvidence.RecommendationAllowed,
                signalEvidence.DataQualityStatus,
                signalEvidence.ReasonCodes,
                history
                    .Select(x => new InventoryHistoryItemDto(
                        x.MovementId,
                        x.TipPromene,
                        EnsureUtc(x.Datum),
                        x.Kolicina,
                        x.Iznos,
                        x.BrojDokumenta,
                        x.KorisnikIme,
                        x.DataOrigin,
                        x.StoreId,
                        ResolveLookup(storeNameMap, x.StoreId),
                        x.SupplierId,
                        ResolveLookup(supplierNameMap, x.SupplierId),
                        x.StaraCena,
                        x.NovaCena,
                        x.Komentar))
                    .ToList());

            return Results.Ok(detail);
        })
        .WithName("GetInventoryItemDetail");

        group.MapPost("/export", async (
            HttpContext httpContext,
            IConfiguration configuration,
            InventoryExportRequestDto dto,
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            IDocumentDownloadTokenService tokenService,
            IOptions<DocumentExportOptions> options,
            CancellationToken ct) =>
        {
            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var context,
                    out var rejected))
            {
                return rejected!;
            }

            var items = await BuildInventoryDatasetAsync(db, analyticsDb, dto.StoreId, dto.SupplierId, dto.Search, dto.SortBy, ct);
            var request = BuildDocumentRequest(items, dto, preview: false);
            var result = await documentService.GenerateAsync(request, context, ct);
            var response = ToDocumentResponse(result, tokenService, options.Value);

            return result.IsAsync
                ? Results.Accepted(response.StatusUrl, response)
                : Results.Ok(response);
        })
        .WithName("ExportInventoryReport")
        .RequireRateLimiting("writes");

        group.MapPost("/print-preview", async (
            HttpContext httpContext,
            IConfiguration configuration,
            InventoryExportRequestDto dto,
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            IDocumentDownloadTokenService tokenService,
            IOptions<DocumentExportOptions> options,
            CancellationToken ct) =>
        {
            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var context,
                    out var rejected))
            {
                return rejected!;
            }

            var items = await BuildInventoryDatasetAsync(db, analyticsDb, dto.StoreId, dto.SupplierId, dto.Search, dto.SortBy, ct);
            var result = await documentService.GenerateAsync(
                BuildDocumentRequest(items, dto, preview: true),
                context,
                ct);

            var printToken = tokenService.Create(result.DocumentId, DateTime.UtcNow.AddMinutes(options.Value.SignedUrlTtlMinutes));
            return Results.Ok(new DocumentOperationResponseDto
            {
                DocumentId = result.DocumentId,
                BatchId = result.BatchId,
                Status = result.Status,
                IsAsync = result.IsAsync,
                FileName = result.FileName,
                MimeType = result.MimeType,
                SizeBytes = result.SizeBytes,
                CreatedAtUtc = result.CreatedAtUtc,
                CompletedAtUtc = result.CompletedAtUtc,
                ExpiresAtUtc = result.ExpiresAtUtc,
                StatusUrl = $"/api/exports/{result.DocumentId}/status",
                PrintUrl = $"/api/documents/{result.DocumentId}/print?token={printToken}"
            });
        })
        .WithName("PreviewInventoryReport")
        .RequireRateLimiting("writes");

        group.MapPost("/print-blank", async (
            HttpContext httpContext,
            IConfiguration configuration,
            string? orientation,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            IDocumentDownloadTokenService tokenService,
            IOptions<DocumentExportOptions> options,
            CancellationToken ct) =>
        {
            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var context,
                    out var rejected))
            {
                return rejected!;
            }

            var result = await documentService.GenerateAsync(
                BuildBlankDocumentRequest(orientation),
                context,
                ct);

            var printToken = tokenService.Create(result.DocumentId, DateTime.UtcNow.AddMinutes(options.Value.SignedUrlTtlMinutes));
            return Results.Ok(new DocumentOperationResponseDto
            {
                DocumentId = result.DocumentId,
                BatchId = result.BatchId,
                Status = result.Status,
                IsAsync = result.IsAsync,
                FileName = result.FileName,
                MimeType = result.MimeType,
                SizeBytes = result.SizeBytes,
                CreatedAtUtc = result.CreatedAtUtc,
                CompletedAtUtc = result.CompletedAtUtc,
                ExpiresAtUtc = result.ExpiresAtUtc,
                StatusUrl = $"/api/exports/{result.DocumentId}/status",
                PrintUrl = $"/api/documents/{result.DocumentId}/print?token={printToken}"
            });
        })
        .WithName("PrintBlankInventoryForm")
        .RequireRateLimiting("writes");

        group.MapGet("/store-comparison", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            int[]? compareStoreIds,
            int? supplierId,
            string? search,
            string? dataScope = null,
            CancellationToken ct = default) =>
        {
            var comparison = await GetInventoryStoreComparisonAsync(cache, db, analyticsDb, compareStoreIds, supplierId, search, ct, dataScope);
            return Results.Ok(comparison);
        })
        .WithName("GetInventoryStoreComparison");

        group.MapGet("/action-suggestions", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            IInventoryActionDecisionService actionDecisionService,
            int? storeId,
            int? supplierId,
            string? search,
            string? dataScope = null,
            CancellationToken ct = default) =>
        {
            var workflow = await GetInventoryActionWorkflowAsync(cache, db, analyticsDb, actionDecisionService, storeId, supplierId, search, ct, dataScope);
            return Results.Ok(workflow);
        })
        .WithName("GetInventoryActionSuggestions");

        group.MapPost("/action-suggestions/{suggestionKey}/decision", async (
            string suggestionKey,
            InventoryActionDecisionRequestDto dto,
            IInventoryActionDecisionService actionDecisionService,
            IDocumentUserContextAccessor userContextAccessor,
            CancellationToken ct) =>
        {
            var user = userContextAccessor.GetCurrent();
            var saved = await actionDecisionService.UpsertAsync(
                new InventoryActionDecisionUpsertRequest(
                    suggestionKey,
                    dto.ActionType,
                    dto.Status,
                    dto.Note,
                    user.UserId,
                    user.UserName),
                ct);

            return Results.Ok(new
            {
                saved.SuggestionKey,
                saved.ActionType,
                saved.Status,
                saved.Note,
                saved.UpdatedAtUtc,
                saved.UpdatedByUserName
            });
        })
        .WithName("SaveInventoryActionDecision")
        .RequireRateLimiting("writes");

        group.MapGet("/report-schedules", async (
            IInventoryReportScheduleService scheduleService,
            CancellationToken ct) =>
        {
            var schedules = await scheduleService.ListAsync(ct);
            return Results.Ok(schedules.Select(MapScheduleDto));
        })
        .WithName("GetInventoryReportSchedules");

        group.MapPost("/report-schedules", async (
            InventoryReportScheduleUpsertDto dto,
            IInventoryReportScheduleService scheduleService,
            IDocumentUserContextAccessor userContextAccessor,
            CancellationToken ct) =>
        {
            var user = userContextAccessor.GetCurrent();
            var saved = await scheduleService.UpsertAsync(
                null,
                new InventoryReportScheduleUpsertRequest(
                    dto.Name,
                    dto.IsEnabled,
                    dto.Frequency,
                    dto.DayOfWeek,
                    dto.RunAtLocalTime,
                    dto.TimeZoneId,
                    dto.Format,
                    dto.Orientation,
                    dto.IncludeFiltersAndMetadata,
                    dto.RecipientsCsv,
                    dto.Subject,
                    dto.Search,
                    dto.StoreId,
                    dto.SupplierId,
                    dto.SortBy,
                    user.UserId,
                    user.UserName),
                ct);

            return Results.Ok(MapScheduleDto(saved));
        })
        .WithName("CreateInventoryReportSchedule")
        .RequireRateLimiting("writes");

        group.MapPut("/report-schedules/{id:long}", async (
            long id,
            InventoryReportScheduleUpsertDto dto,
            IInventoryReportScheduleService scheduleService,
            IDocumentUserContextAccessor userContextAccessor,
            CancellationToken ct) =>
        {
            var user = userContextAccessor.GetCurrent();
            var saved = await scheduleService.UpsertAsync(
                id,
                new InventoryReportScheduleUpsertRequest(
                    dto.Name,
                    dto.IsEnabled,
                    dto.Frequency,
                    dto.DayOfWeek,
                    dto.RunAtLocalTime,
                    dto.TimeZoneId,
                    dto.Format,
                    dto.Orientation,
                    dto.IncludeFiltersAndMetadata,
                    dto.RecipientsCsv,
                    dto.Subject,
                    dto.Search,
                    dto.StoreId,
                    dto.SupplierId,
                    dto.SortBy,
                    user.UserId,
                    user.UserName),
                ct);

            return Results.Ok(MapScheduleDto(saved));
        })
        .WithName("UpdateInventoryReportSchedule")
        .RequireRateLimiting("writes");

        group.MapPost("/report-schedules/{id:long}/run-now", async (
            long id,
            HttpContext httpContext,
            IConfiguration configuration,
            IInventoryReportScheduleService scheduleService,
            InventoryReportDeliveryService deliveryService,
            IDocumentUserContextAccessor userContextAccessor,
            CancellationToken ct) =>
        {
            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var user,
                    out var rejected))
            {
                return rejected!;
            }

            var schedule = await scheduleService.GetByIdAsync(id, ct);
            if (schedule is null)
            {
                return Results.NotFound(new { message = $"Raspored sa Id={id} nije pronadjen." });
            }

            var result = await deliveryService.RunAsync(schedule, user.UserId, user.UserName, manualTrigger: true, ct);
            await scheduleService.MarkRunResultAsync(id, result, ct);

            return Results.Ok(new InventoryScheduleRunResponseDto(
                result.Success,
                result.Status,
                result.Message,
                result.DocumentId,
                result.ExecutedAtUtc));
        })
        .WithName("RunInventoryReportScheduleNow")
        .RequireRateLimiting("writes");
    }

    public static async Task<InventoryInsightsDto> GetInventoryInsightsAsync(
        IAnalyticsCacheService cache,
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
        int? storeId,
        int? supplierId,
        string? search,
        string? sortBy,
        CancellationToken ct,
        string? dataScope = null)
    {
        var items = await GetCachedInventoryDatasetAsync(cache, db, analyticsDb, storeId, supplierId, search, null, applyAbcClassification: true, ct, dataScope);
        var articleIds = items.Select(item => item.Id).ToArray();
        var soldUnitsByArticle = await LoadSoldUnitsByArticleAsync(db, articleIds, storeId, 30, ct);
        var movementWindowStatsByArticle = await LoadInventorySignalWindowStatsAsync(analyticsDb, articleIds, storeId, 30, ct);
        return BuildInsights(items, soldUnitsByArticle, movementWindowStatsByArticle);
    }

    public static Task<InventoryStoreComparisonDto> GetInventoryStoreComparisonAsync(
        IAnalyticsCacheService cache,
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
        int[]? compareStoreIds,
        int? supplierId,
        string? search,
        CancellationToken ct,
        string? dataScope = null) =>
        BuildStoreComparisonAsync(cache, db, analyticsDb, compareStoreIds, supplierId, search, ct, dataScope);

    public static Task<InventoryActionWorkflowDto> GetInventoryActionWorkflowAsync(
        IAnalyticsCacheService cache,
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
        IInventoryActionDecisionService actionDecisionService,
        int? storeId,
        int? supplierId,
        string? search,
        CancellationToken ct,
        string? dataScope = null) =>
        BuildActionWorkflowAsync(cache, db, analyticsDb, actionDecisionService, storeId, supplierId, search, ct, dataScope);

    private static Task<List<InventoryDatasetItem>> GetCachedInventoryDatasetAsync(
        IAnalyticsCacheService cache,
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
        int? storeId,
        int? supplierId,
        string? search,
        IReadOnlyCollection<int>? storeIds,
        bool applyAbcClassification,
        CancellationToken ct,
        string? dataScope = null)
    {
        var normalizedStoreIds = storeIds?
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var cacheKey = AnalyticsCacheKeys.InventoryDataset(storeId, supplierId, search, normalizedStoreIds, applyAbcClassification, dataScope);

        return cache.GetOrSetAsync(
            cacheKey,
            () => BuildInventoryDatasetAsync(db, analyticsDb, storeId, supplierId, search, null, ct, normalizedStoreIds, applyAbcClassification, dataScope),
            CacheExpiration.Short,
            ct);
    }

    private static IQueryable<Domain.Model.Artikli> ApplyInventoryFilters(
        IQueryable<Domain.Model.Artikli> query,
        int? storeId,
        int? supplierId,
        string? search,
        IReadOnlyCollection<int>? storeIds = null,
        string? dataScope = null)
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        if (storeId.HasValue)
        {
            query = query.Where(a => a.IDObjekat == storeId.Value);
        }
        else if (storeIds is { Count: > 0 })
        {
            var normalizedStoreIds = storeIds.Where(id => id > 0).Distinct().ToArray();
            if (normalizedStoreIds.Length > 0)
            {
                query = query.Where(a => a.IDObjekat.HasValue && normalizedStoreIds.Contains(a.IDObjekat.Value));
            }
        }

        if (supplierId.HasValue)
            query = query.Where(a => a.IDDobavljac == supplierId.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => (a.Naziv ?? string.Empty).Contains(search) || (a.PLU ?? string.Empty).Contains(search));
        if (importedOnly)
            query = query.Where(a => a.DataOrigin == "access");
        else if (existingOnly)
            query = query.Where(a => a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "");

        return query;
    }

    private static IQueryable<Domain.Model.Artikli> ApplyInventorySorting(
        IQueryable<Domain.Model.Artikli> query,
        string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "naziv" => query.OrderBy(a => a.Naziv).ThenBy(a => a.Id),
            "vrednost" => query.OrderByDescending(a => (a.NabavnaCena ?? 0m) * ((a.Kolicina ?? 0) > 0 ? (a.Kolicina ?? 0) : 0)).ThenBy(a => a.Naziv),
            "azuriranje" => query.OrderByDescending(a => a.UpdatedAt).ThenBy(a => a.Naziv),
            _ => query.OrderByDescending(a => a.Kolicina ?? 0).ThenBy(a => a.Naziv)
        };
    }

    private static async Task<List<InventoryDatasetItem>> BuildInventoryDatasetAsync(
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
        int? storeId,
        int? supplierId,
        string? search,
        string? sortBy,
        CancellationToken ct,
        IReadOnlyCollection<int>? storeIds = null,
        bool applyAbcClassification = true,
        string? dataScope = null)
    {
        var baseItems = await ApplyInventorySorting(
                ApplyInventoryFilters(db.Artikli.AsNoTracking(), storeId, supplierId, search, storeIds, dataScope),
                sortBy)
            .Select(a => new InventoryArticleProjection(
                a.Id,
                a.PLU,
                a.Naziv,
                a.Kolicina ?? 0,
                a.MinimalnaKolicina ?? 0,
                a.NabavnaCena ?? 0m,
                a.IDObjekat,
                a.IDDobavljac,
                a.Kategorija,
                a.Pol,
                a.Materijal,
                a.UpdatedAt))
            .ToListAsync(ct);

        if (baseItems.Count == 0)
        {
            return [];
        }

        var itemIds = baseItems.Select(x => x.Id).ToList();
        var storeNameMap = await LoadStoreNamesAsync(analyticsDb, baseItems.Select(x => x.StoreId), ct);
        var supplierNameMap = await LoadSupplierNamesAsync(analyticsDb, db, baseItems.Select(x => x.SupplierId), ct);
        var cutoff30d = DateTime.UtcNow.AddDays(-30);
        var movementStats = await LoadMovementStatsAsync(analyticsDb, itemIds, cutoff30d, ct);

        var items = baseItems
            .Select(item =>
            {
                movementStats.TryGetValue(item.Id, out var movement);
                DateTime? lastMovementAt = movement is null ? null : EnsureUtc(movement.LastMovementAt);
                var daysSinceMovement = ResolveDaysSinceMovement(lastMovementAt, item.UpdatedAt);
                var (agingBucket, agingLabel) = ResolveAging(daysSinceMovement);

                return new InventoryDatasetItem(
                    item.Id,
                    item.Plu,
                    item.Naziv,
                    item.Quantity,
                    item.Minimum,
                    item.UnitCost,
                    item.UnitCost * Math.Max(item.Quantity, 0),
                    item.StoreId,
                    ResolveLookup(storeNameMap, item.StoreId),
                    item.SupplierId,
                    ResolveLookup(supplierNameMap, item.SupplierId),
                    item.Kategorija,
                    item.Pol,
                    item.Materijal,
                    EnsureUtc(item.UpdatedAt),
                    lastMovementAt,
                    movement?.MovementCount30d ?? 0,
                    daysSinceMovement,
                    agingBucket,
                    agingLabel,
                    "C");
            })
            .ToList();

        return applyAbcClassification ? ApplyAbcClassification(items) : items;
    }

    private static async Task<Dictionary<int, InventoryMovementStats>> LoadMovementStatsAsync(
        IAnalyticsDbContext analyticsDb,
        List<int> itemIds,
        DateTime cutoff30d,
        CancellationToken ct)
    {
        var stats = new Dictionary<int, InventoryMovementStats>(itemIds.Count);
        foreach (var batch in itemIds.Distinct().Chunk(MovementStatsBatchSize))
        {
            var batchStats = await analyticsDb.InventoryMovementFacts
                .AsNoTracking()
                .Where(x => x.ArtikalId.HasValue && batch.Contains(x.ArtikalId.Value))
                .GroupBy(x => x.ArtikalId!.Value)
                .Select(g => new
                {
                    ArtikalId = g.Key,
                    LastMovementAt = g.Max(x => x.Datum),
                    MovementCount30d = g.Count(x => x.Datum >= cutoff30d)
                })
                .ToListAsync(ct);

            foreach (var row in batchStats)
            {
                stats[row.ArtikalId] = new InventoryMovementStats(row.LastMovementAt, row.MovementCount30d);
            }
        }

        return stats;
    }

    private static async Task<Dictionary<int, int>> LoadSoldUnitsByArticleAsync(
        ITrendplusDbContext db,
        int[] articleIds,
        int? storeId,
        int lookbackDays,
        CancellationToken ct)
    {
        if (articleIds.Length == 0)
        {
            return [];
        }

        var fromDate = DateTime.UtcNow.AddDays(-Math.Max(lookbackDays, 1));

        var grouped = await (
            from pz in db.ProdajaZaglavlja.AsNoTracking()
            join ps in db.ProdajaStavke.AsNoTracking() on pz.Id equals ps.IdProdaja
            where articleIds.Contains(ps.IdArtikal)
                  && pz.DatumProdaje >= fromDate
                  && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
            group ps by ps.IdArtikal
            into g
            select new
            {
                ProductId = g.Key,
                UnitsSold = g.Sum(x => x.Kolicina)
            })
            .ToListAsync(ct);

        return grouped.ToDictionary(x => x.ProductId, x => x.UnitsSold);
    }

    private static async Task<Dictionary<int, InventorySignalWindowStats>> LoadInventorySignalWindowStatsAsync(
        IAnalyticsDbContext analyticsDb,
        int[] articleIds,
        int? storeId,
        int lookbackDays,
        CancellationToken ct)
    {
        if (articleIds.Length == 0)
        {
            return [];
        }

        var fromDate = DateTime.UtcNow.AddDays(-Math.Max(lookbackDays, 1));
        var inboundTypes = TipPromeneConstants.UlazTypes.ToArray();
        var stats = new Dictionary<int, InventorySignalWindowStats>(articleIds.Length);

        foreach (var batch in articleIds.Distinct().Chunk(MovementStatsBatchSize))
        {
            var batchStats = await analyticsDb.InventoryMovementFacts
                .AsNoTracking()
                .Where(x => x.ArtikalId.HasValue
                    && batch.Contains(x.ArtikalId.Value)
                    && x.Datum >= fromDate
                    && (!storeId.HasValue || x.StoreId == storeId.Value))
                .GroupBy(x => x.ArtikalId!.Value)
                .Select(g => new
                {
                    ArtikalId = g.Key,
                    NetMovementUnits = g.Sum(x => x.Kolicina ?? 0),
                    InboundUnits = g.Where(x => inboundTypes.Contains(x.TipPromene)).Sum(x => Math.Max(x.Kolicina ?? 0, 0))
                })
                .ToListAsync(ct);

            foreach (var row in batchStats)
            {
                stats[row.ArtikalId] = new InventorySignalWindowStats(row.NetMovementUnits, row.InboundUnits);
            }
        }

        return stats;
    }

    private static List<InventoryDatasetItem> ApplyAbcClassification(List<InventoryDatasetItem> items)
    {
        var totalValue = items.Sum(x => x.EstimatedValue);
        if (totalValue <= 0)
        {
            return items.Select(x => x with { AbcClass = "C" }).ToList();
        }

        var runningValue = 0m;
        var abcById = new Dictionary<int, string>();
        foreach (var item in items.OrderByDescending(x => x.EstimatedValue).ThenBy(x => x.Naziv))
        {
            runningValue += item.EstimatedValue;
            var share = runningValue / totalValue;
            abcById[item.Id] = share <= 0.80m ? "A" : share <= 0.95m ? "B" : "C";
        }

        return items.Select(item => item with { AbcClass = abcById.GetValueOrDefault(item.Id, "C") }).ToList();
    }

    private static InventoryInsightsDto BuildInsights(
        List<InventoryDatasetItem> items,
        IReadOnlyDictionary<int, int> soldUnitsByArticle,
        IReadOnlyDictionary<int, InventorySignalWindowStats> movementWindowStatsByArticle)
    {
        var totalValue = items.Sum(x => x.EstimatedValue);

        var aging = items
            .GroupBy(x => new { x.AgingBucket, x.AgingLabel })
            .Select(g => new InventoryAgingBucketDto(
                g.Key.AgingBucket,
                g.Key.AgingLabel,
                g.Count(),
                g.Sum(x => x.Quantity),
                Math.Round(g.Sum(x => x.EstimatedValue), 2)))
            .OrderBy(x => AgingOrder(x.BucketKey))
            .ToList();

        var abc = items
            .GroupBy(x => x.AbcClass)
            .Select(g =>
            {
                var bucketValue = g.Sum(x => x.EstimatedValue);
                return new InventoryAbcBucketDto(
                    g.Key,
                    $"Klasa {g.Key}",
                    g.Count(),
                    Math.Round(bucketValue, 2),
                    totalValue <= 0 ? 0 : Math.Round(bucketValue / totalValue * 100m, 1));
            })
            .OrderBy(x => x.BucketKey)
            .ToList();

        return new InventoryInsightsDto(
            items.Count,
            Math.Round(totalValue, 2),
            aging,
            abc,
            items
                .OrderByDescending(x => x.DaysSinceMovement)
                .ThenByDescending(x => x.EstimatedValue)
                .Take(5)
                .Select(item => ToInsightItem(item, soldUnitsByArticle, movementWindowStatsByArticle))
                .ToList(),
            items
                .OrderByDescending(x => x.EstimatedValue)
                .ThenByDescending(x => x.Quantity)
                .Take(5)
                .Select(item => ToInsightItem(item, soldUnitsByArticle, movementWindowStatsByArticle))
                .ToList());
    }

    private static InventoryInsightItemDto ToInsightItem(
        InventoryDatasetItem item,
        IReadOnlyDictionary<int, int> soldUnitsByArticle,
        IReadOnlyDictionary<int, InventorySignalWindowStats> movementWindowStatsByArticle)
    {
        var reorderGap = Math.Max(item.Minimum - item.Quantity, 0);
        var soldUnits30d = soldUnitsByArticle.TryGetValue(item.Id, out var units) ? units : 0;
        var movementWindowStats = movementWindowStatsByArticle.TryGetValue(item.Id, out var stats)
            ? stats
            : new InventorySignalWindowStats(0, 0);
        var signalEvidence = ComputeInventorySignalEvidence(item, soldUnits30d, movementWindowStats);
        return new InventoryInsightItemDto(
            item.Id,
            item.Plu,
            item.Naziv,
            item.SupplierName,
            item.StoreName,
            item.Quantity,
            item.Minimum,
            reorderGap,
            item.EstimatedValue,
            item.DaysSinceMovement,
            item.AgingBucket,
            item.AgingLabel,
            item.AbcClass,
            ResolveStockState(item.Quantity, item.Minimum),
            signalEvidence.StockCoverDays,
            signalEvidence.StockCoverStatus,
            signalEvidence.StockCoverStatusLabel,
            signalEvidence.SellThroughRatio,
            signalEvidence.SellThroughStatus,
            signalEvidence.SellThroughStatusLabel,
            signalEvidence.SignalConfidencePct,
            signalEvidence.RecommendationAllowed,
            signalEvidence.DataQualityStatus,
            signalEvidence.ReasonCodes);
    }

    private static async Task<InventoryStoreComparisonDto> BuildStoreComparisonAsync(
        IAnalyticsCacheService cache,
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
        int[]? compareStoreIds,
        int? supplierId,
        string? search,
        CancellationToken ct,
        string? dataScope = null)
    {
        var selectedStoreIds = (compareStoreIds ?? [])
            .Distinct()
            .Where(id => id > 0)
            .ToList();

        if (selectedStoreIds.Count == 0)
        {
            selectedStoreIds = await ApplyInventoryFilters(db.Artikli.AsNoTracking(), null, supplierId, search, dataScope: dataScope)
                .Where(item => item.IDObjekat.HasValue)
                .GroupBy(item => item.IDObjekat!.Value)
                .Select(group => new
                {
                    StoreId = group.Key,
                    EstimatedValue = group.Sum(item => (item.NabavnaCena ?? 0m) * ((item.Kolicina ?? 0) > 0 ? (item.Kolicina ?? 0) : 0))
                })
                .OrderByDescending(group => group.EstimatedValue)
                .Take(3)
                .Select(group => group.StoreId)
                .ToListAsync(ct);
        }

        var selectedItems = selectedStoreIds.Count == 0
            ? []
            : await GetCachedInventoryDatasetAsync(cache, db, analyticsDb, null, supplierId, search, selectedStoreIds, applyAbcClassification: false, ct, dataScope);

        var storeNames = await LoadStoreNamesAsync(analyticsDb, selectedStoreIds.Select(static id => (int?)id), ct);
        var stores = selectedStoreIds
            .Select(storeId =>
            {
                var items = selectedItems.Where(item => item.StoreId == storeId).ToList();
                var totalSku = items.Count;
                var totalOnHand = items.Sum(item => item.Quantity);
                var lowStock = items.Count(item => item.Quantity > 0 && item.Quantity <= item.Minimum);
                var outOfStock = items.Count(item => item.Quantity <= 0);
                var critical = items.Count(item => ResolveStockState(item.Quantity, item.Minimum) == "critical");
                var healthy = items.Count(item => ResolveStockState(item.Quantity, item.Minimum) == "healthy");
                var estimatedValue = items.Sum(item => item.EstimatedValue);
                var stale = items.Count(item => item.DaysSinceMovement >= 90);
                var healthyShare = totalSku == 0 ? 0 : Math.Round((decimal)healthy / totalSku * 100m, 1);

                return new InventoryStoreComparisonItemDto(
                    storeId,
                    storeNames.GetValueOrDefault(storeId, $"Objekat #{storeId}"),
                    totalSku,
                    totalOnHand,
                    lowStock,
                    outOfStock,
                    critical,
                    stale,
                    Math.Round(estimatedValue, 2),
                    totalSku == 0 ? 0 : Math.Round((decimal)totalOnHand / totalSku, 1),
                    healthyShare);
            })
            .Where(item => item.TotalSku > 0)
            .OrderByDescending(item => item.EstimatedValue)
            .ToList();

        var sharedRisks = selectedItems
            .Where(item => item.Quantity <= item.Minimum)
            .GroupBy(NormalizeSkuKey)
            .Select(group =>
            {
                var impactedStores = group
                    .Select(item => item.StoreName ?? "Nepoznata lokacija")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList();
                var lead = group
                    .OrderByDescending(item => Math.Max(item.Minimum - item.Quantity, 0))
                    .ThenByDescending(item => item.EstimatedValue)
                    .First();

                return new InventoryStoreComparisonFocusDto(
                    group.Key,
                    lead.Naziv,
                    impactedStores.Count,
                    impactedStores);
            })
            .Where(item => item.StoreCoverage > 1)
            .OrderByDescending(item => item.StoreCoverage)
            .ThenBy(item => item.Label)
            .Take(6)
            .ToList();

        var worstStore = stores.OrderByDescending(item => item.CriticalCount).ThenByDescending(item => item.Stale90PlusCount).FirstOrDefault();
        var bestStore = stores.OrderByDescending(item => item.HealthySharePct).ThenByDescending(item => item.EstimatedValue).FirstOrDefault();
        var summary = stores.Count == 0
            ? "Nema dovoljno podataka za poređenje lokacija."
            : worstStore is null || bestStore is null
                ? $"Prikazane su {stores.Count} lokacije za poređenje."
                : $"Najveci operativni pritisak je u lokaciji {worstStore.StoreName}, dok {bestStore.StoreName} trenutno ima najbolji udeo zdravih SKU.";

        return new InventoryStoreComparisonDto(
            DateTime.UtcNow,
            stores,
            sharedRisks,
            summary);
    }

    private static async Task<InventoryActionWorkflowDto> BuildActionWorkflowAsync(
        IAnalyticsCacheService cache,
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
        IInventoryActionDecisionService actionDecisionService,
        int? storeId,
        int? supplierId,
        string? search,
        CancellationToken ct,
        string? dataScope = null)
    {
        var items = await GetCachedInventoryDatasetAsync(cache, db, analyticsDb, storeId, supplierId, search, null, applyAbcClassification: true, ct, dataScope);
        var decisions = await actionDecisionService.ListAsync(ct);
        var articleIds = items.Select(item => item.Id).ToArray();
        var soldUnitsByArticle = await LoadSoldUnitsByArticleAsync(db, articleIds, storeId, 30, ct);
        var movementWindowStatsByArticle = await LoadInventorySignalWindowStatsAsync(analyticsDb, articleIds, storeId, 30, ct);
        var suggestions = new List<InventoryActionSuggestionDto>();

        foreach (var item in items.Where(item => item.Quantity <= item.Minimum && item.DaysSinceMovement <= 60))
        {
            var key = BuildSuggestionKey("dopuna", item, item.StoreId, null);
            suggestions.Add(ToSuggestion(
                decisions,
                key,
                "dopuna",
                item.Quantity <= 0 ? "critical" : "high",
                $"Dopuna za {item.Naziv}",
                item.Quantity <= 0
                    ? "Artikal je bez zalihe ili na nuli u lokaciji sa aktivnim minimumom."
                    : "Artikal je ispod minimuma i jos uvek pokazuje aktivno kretanje.",
                item,
                item.StoreName,
                null,
                Math.Max(item.Minimum - item.Quantity, 1),
                item.EstimatedValue,
                soldUnitsByArticle,
                movementWindowStatsByArticle));
        }

        foreach (var item in items.Where(item => item.Quantity >= Math.Max(item.Minimum * 2, 8) && item.DaysSinceMovement >= 60 && item.DaysSinceMovement < 90))
        {
            var key = BuildSuggestionKey("markdown", item, item.StoreId, null);
            suggestions.Add(ToSuggestion(
                decisions,
                key,
                "markdown",
                item.AbcClass == "A" ? "high" : "medium",
                $"Markdown predlog za {item.Naziv}",
                "Zaliha je visoka u odnosu na minimum, a aging ulazi u zonu sporog obrta.",
                item,
                item.StoreName,
                null,
                Math.Max(item.Quantity - item.Minimum, 1),
                item.EstimatedValue,
                soldUnitsByArticle,
                movementWindowStatsByArticle));
        }

        foreach (var item in items.Where(item => item.Quantity >= Math.Max(item.Minimum, 3) && item.DaysSinceMovement >= 90))
        {
            var key = BuildSuggestionKey("clearance", item, item.StoreId, null);
            suggestions.Add(ToSuggestion(
                decisions,
                key,
                "clearance",
                item.AbcClass == "A" ? "high" : "medium",
                $"Clearance lista za {item.Naziv}",
                "Artikal je 90+ dana bez kretanja i vezuje kapital koji treba osloboditi.",
                item,
                item.StoreName,
                null,
                item.Quantity,
                item.EstimatedValue,
                soldUnitsByArticle,
                movementWindowStatsByArticle));
        }

        foreach (var group in items.Where(item => item.StoreId.HasValue).GroupBy(NormalizeSkuKey))
        {
            var sources = group
                .Where(item => item.Quantity > Math.Max(item.Minimum * 2, item.Minimum + 2))
                .OrderByDescending(item => item.Quantity - item.Minimum)
                .ToList();
            var destinations = group
                .Where(item => item.Quantity < item.Minimum)
                .OrderByDescending(item => item.Minimum - item.Quantity)
                .ToList();

            foreach (var destination in destinations)
            {
                var source = sources.FirstOrDefault(candidate => candidate.StoreId != destination.StoreId);
                if (source is null || !source.StoreId.HasValue || !destination.StoreId.HasValue)
                {
                    continue;
                }

                var safeSourceCover = Math.Max(source.Minimum, 1);
                var sourceExcess = Math.Max(source.Quantity - safeSourceCover, 0);
                var destinationNeed = Math.Max(destination.Minimum - destination.Quantity, 0);
                var qty = Math.Min(sourceExcess, destinationNeed);
                if (qty <= 0)
                {
                    continue;
                }

                var key = BuildTransferSuggestionKey(group.Key, source.StoreId.Value, destination.StoreId.Value);
                suggestions.Add(ToSuggestion(
                    decisions,
                    key,
                    "transfer",
                    destination.Quantity <= 0 ? "critical" : "high",
                    $"Transfer {source.StoreName} -> {destination.StoreName}",
                    "Jedna lokacija ima siguran visak, dok druga pada ispod minimuma za isti SKU.",
                    destination,
                    source.StoreName,
                    destination.StoreName,
                    qty,
                    Math.Round(destination.UnitCost * qty, 2),
                    soldUnitsByArticle,
                    movementWindowStatsByArticle));
            }
        }

        var distinctSuggestions = suggestions
            .GroupBy(item => item.SuggestionKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => ActionPriorityOrder(item.Priority))
            .ThenBy(item => item.ActionType)
            .ThenByDescending(item => item.EstimatedValue)
            .Take(24)
            .ToList();

        return new InventoryActionWorkflowDto(
            DateTime.UtcNow,
            distinctSuggestions.Count(item => item.Status == "pending"),
            distinctSuggestions.Count(item => item.Status == "approved"),
            distinctSuggestions.Count(item => item.Status == "deferred"),
            distinctSuggestions.Count(item => item.Status == "closed"),
            distinctSuggestions);
    }

    private static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized switch
        {
            "imported" or "access" => "imported",
            "existing" => "existing",
            _ => "all"
        };
    }

    private static InventoryActionSuggestionDto ToSuggestion(
        IReadOnlyDictionary<string, InventoryActionDecisionDefinition> decisions,
        string key,
        string actionType,
        string priority,
        string label,
        string reason,
        InventoryDatasetItem item,
        string? fromStoreName,
        string? toStoreName,
        int suggestedQty,
        decimal estimatedValue,
        Dictionary<int, int> soldUnitsByArticle,
        Dictionary<int, InventorySignalWindowStats> movementWindowStatsByArticle)
    {
        decisions.TryGetValue(key, out var decision);
        var signalEvidence = ComputeInventorySignalEvidence(item,
            soldUnitsByArticle.TryGetValue(item.Id, out var soldUnits30d) ? soldUnits30d : 0,
            movementWindowStatsByArticle.TryGetValue(item.Id, out var movementWindowStats)
                ? movementWindowStats
                : new InventorySignalWindowStats(0, 0));
        return new InventoryActionSuggestionDto(
            key,
            actionType,
            priority,
            label,
            reason,
            NormalizeDecisionStatus(decision?.Status),
            item.Id,
            item.Plu,
            item.Naziv,
            fromStoreName,
            toStoreName,
            suggestedQty,
            Math.Round(estimatedValue, 2),
            item.DaysSinceMovement,
            decision?.Note,
            decision?.UpdatedAtUtc,
            signalEvidence.SignalConfidencePct,
            signalEvidence.RecommendationAllowed,
            signalEvidence.DataQualityStatus,
            signalEvidence.ReasonCodes);
    }

    private sealed record InventorySignalEvidenceSnapshot(
        decimal? StockCoverDays,
        string StockCoverStatus,
        string StockCoverStatusLabel,
        decimal? SellThroughRatio,
        string SellThroughStatus,
        string SellThroughStatusLabel,
        decimal SignalConfidencePct,
        bool RecommendationAllowed,
        string DataQualityStatus,
        IReadOnlyList<string> ReasonCodes);

    private static InventorySignalEvidenceSnapshot ComputeInventorySignalEvidence(
        InventoryDatasetItem item,
        int soldUnits30d,
        InventorySignalWindowStats movementWindowStats)
    {
        var openingStockUnits = Math.Max(item.Quantity - movementWindowStats.NetMovementUnits, 0);
        var hasReliableSellThroughInputs = openingStockUnits > 0 || movementWindowStats.InboundUnits > 0;
        var avgDailySalesUnits = Math.Round(soldUnits30d / 30m, 4, MidpointRounding.AwayFromZero);
        var hasSufficientData = soldUnits30d > 0 || item.Quantity > 0 || hasReliableSellThroughInputs;
        var signalDataQuality = soldUnits30d > 0 && hasReliableSellThroughInputs
            ? "good"
            : hasSufficientData
                ? "warning"
                : "insufficient_data";

        var signal = ComputeInventorySignalEvidence(
            item.Quantity,
            avgDailySalesUnits,
            soldUnits30d,
            openingStockUnits,
            movementWindowStats.InboundUnits,
            signalDataQuality,
            hasSufficientData);

        var reasonCodes = signal.ReasonCodes.ToList();
        if (signal.StockCoverStatus == InventorySignalCalculator.StockCoverOutOfStockRisk)
        {
            reasonCodes.Add("replenish_needed");
        }

        if (signal.StockCoverStatus is InventorySignalCalculator.StockCoverSlowStock or InventorySignalCalculator.StockCoverNoVelocity)
        {
            reasonCodes.Add("slow_stock");
        }

        return signal with
        {
            ReasonCodes = reasonCodes.Distinct(StringComparer.Ordinal).ToList()
        };
    }

    private static InventorySignalEvidenceSnapshot ComputeInventorySignalEvidence(
        int currentOnHandUnits,
        decimal avgDailySalesUnits,
        int soldUnits,
        int? openingStockUnits,
        int? inboundUnits,
        string dataQualityStatus,
        bool hasSufficientData)
    {
        var signal = InventorySignalCalculator.Calculate(
            currentOnHandUnits: currentOnHandUnits,
            avgDailySalesUnits: avgDailySalesUnits,
            soldUnits: soldUnits,
            openingStockUnits: openingStockUnits,
            inboundUnits: inboundUnits,
            dataQualityStatus: dataQualityStatus,
            hasSufficientData: hasSufficientData);

        var reasonCodes = signal.ReasonCodes.ToList();
        if (signal.StockCoverStatus == InventorySignalCalculator.StockCoverOutOfStockRisk)
        {
            reasonCodes.Add("replenish_needed");
        }

        if (signal.StockCoverStatus is InventorySignalCalculator.StockCoverSlowStock or InventorySignalCalculator.StockCoverNoVelocity)
        {
            reasonCodes.Add("slow_stock");
        }

        return new InventorySignalEvidenceSnapshot(
            signal.StockCoverDays,
            signal.StockCoverStatus,
            signal.StockCoverStatusLabel,
            signal.SellThroughRatio,
            signal.SellThroughStatus,
            signal.SellThroughStatusLabel,
            signal.SignalConfidencePct,
            signal.RecommendationAllowed,
            dataQualityStatus,
            reasonCodes.Distinct(StringComparer.Ordinal).ToList());
    }

    private static string NormalizeDecisionStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "approved" => "approved",
            "deferred" => "deferred",
            "closed" => "closed",
            _ => "pending"
        };
    }

    private static int ActionPriorityOrder(string priority)
    {
        return priority switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" => 2,
            _ => 3
        };
    }

    private static string BuildSuggestionKey(string actionType, InventoryDatasetItem item, int? fromStoreId, int? toStoreId)
        => $"{actionType}|{NormalizeSkuKey(item)}|{fromStoreId?.ToString(SerbianCulture) ?? "0"}|{toStoreId?.ToString(SerbianCulture) ?? "0"}";

    private static string BuildTransferSuggestionKey(string normalizedSkuKey, int fromStoreId, int toStoreId)
        => $"transfer|{normalizedSkuKey}|{fromStoreId.ToString(SerbianCulture)}|{toStoreId.ToString(SerbianCulture)}";

    private static string NormalizeSkuKey(InventoryDatasetItem item)
        => !string.IsNullOrWhiteSpace(item.Plu)
            ? item.Plu.Trim().ToUpperInvariant()
            : item.Naziv.Trim().ToUpperInvariant();

    private static InventoryReportScheduleDto MapScheduleDto(InventoryReportScheduleDefinition definition)
    {
        return new InventoryReportScheduleDto
        {
            Id = definition.Id,
            Name = definition.Name,
            IsEnabled = definition.IsEnabled,
            Frequency = definition.Frequency,
            DayOfWeek = definition.DayOfWeek,
            RunAtLocalTime = definition.RunAtLocalTime,
            TimeZoneId = definition.TimeZoneId,
            Format = definition.Format,
            Orientation = definition.Orientation,
            IncludeFiltersAndMetadata = definition.IncludeFiltersAndMetadata,
            RecipientsCsv = definition.RecipientsCsv,
            Subject = definition.Subject,
            Search = definition.Search,
            StoreId = definition.StoreId,
            SupplierId = definition.SupplierId,
            SortBy = definition.SortBy,
            LastRunAtUtc = definition.LastRunAtUtc,
            LastRunStatus = definition.LastRunStatus,
            LastError = definition.LastError,
            LastDocumentId = definition.LastDocumentId
        };
    }

    private static async Task<Dictionary<int, string>> LoadStoreNamesAsync(
        IAnalyticsDbContext analyticsDb,
        IEnumerable<int?> storeIds,
        CancellationToken ct)
    {
        var ids = storeIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await analyticsDb.StoresDim
            .AsNoTracking()
            .Where(x => ids.Contains(x.StoreId))
            .ToDictionaryAsync(x => x.StoreId, x => x.StoreName, ct);
    }

    private static async Task<Dictionary<int, string>> LoadSupplierNamesAsync(
        IAnalyticsDbContext analyticsDb,
        ITrendplusDbContext trendDb,
        IEnumerable<int?> supplierIds,
        CancellationToken ct)
    {
        var ids = supplierIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var names = await analyticsDb.SuppliersDim
            .AsNoTracking()
            .Where(x => ids.Contains(x.SupplierId))
            .ToDictionaryAsync(x => x.SupplierId, x => x.Naziv, ct);

        var missingIds = ids.Where(id => !names.ContainsKey(id)).ToList();
        if (missingIds.Count == 0)
        {
            return names;
        }

        var fallback = await trendDb.Dobavljaci
            .AsNoTracking()
            .Where(x => missingIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Naziv ?? $"Dobavljac #{x.Id}", ct);

        foreach (var entry in fallback)
        {
            names[entry.Key] = entry.Value;
        }

        return names;
    }

    private static int ResolveDaysSinceMovement(DateTime? lastMovementAtUtc, DateTime updatedAt)
    {
        var referenceDate = (lastMovementAtUtc ?? EnsureUtc(updatedAt)).Date;
        var days = (DateTime.UtcNow.Date - referenceDate).Days;
        return Math.Max(days, 0);
    }

    private static (string Bucket, string Label) ResolveAging(int daysSinceMovement)
    {
        return daysSinceMovement switch
        {
            <= 30 => ("0-30", "0-30 dana"),
            <= 60 => ("31-60", "31-60 dana"),
            <= 90 => ("61-90", "61-90 dana"),
            _ => ("90+", "90+ dana")
        };
    }

    private static string ResolveStockState(int quantity, int minimum)
    {
        if (quantity <= 0) return "critical";
        if (quantity <= minimum) return "warning";
        return "healthy";
    }

    private static int AgingOrder(string bucket)
    {
        return bucket switch
        {
            "0-30" => 0,
            "31-60" => 1,
            "61-90" => 2,
            _ => 3
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string? ResolveLookup(Dictionary<int, string> values, int? id)
        => id.HasValue && values.TryGetValue(id.Value, out var value) ? value : null;

    private static DocumentGenerationRequest BuildDocumentRequest(
        List<InventoryDatasetItem> items,
        InventoryExportRequestDto dto,
        bool preview)
    {
        var insights = BuildInsights(items);
        var table = new DocumentTablePayload
        {
            TableKey = "inventory-balance",
            TableTitle = "Bilans stanja",
            Columns =
            [
                new() { Key = "plu", Header = "PLU", DataType = "text" },
                new() { Key = "naziv", Header = "Naziv", DataType = "text" },
                new() { Key = "supplier", Header = "Dobavljac", DataType = "text" },
                new() { Key = "store", Header = "Prodavnica", DataType = "text" },
                new() { Key = "stock", Header = "Status", DataType = "text" },
                new() { Key = "abc", Header = "ABC", DataType = "text" },
                new() { Key = "aging", Header = "Aging", DataType = "text" },
                new() { Key = "quantity", Header = "Kolicina", DataType = "number" },
                new() { Key = "minimum", Header = "Minimum", DataType = "number" },
                new() { Key = "gap", Header = "Gap", DataType = "number" },
                new() { Key = "unitCost", Header = "Nabavna cena", DataType = "currency" },
                new() { Key = "estimatedValue", Header = "Vrednost zalihe", DataType = "currency" },
                new() { Key = "daysSinceMovement", Header = "Dana bez kretanja", DataType = "number" },
                new() { Key = "lastMovementAt", Header = "Poslednje kretanje", DataType = "text" }
            ],
            Rows = items.Select(item =>
            {
                var reorderGap = Math.Max(item.Minimum - item.Quantity, 0);
                return new List<string?>
                {
                    item.Plu,
                    item.Naziv,
                    item.SupplierName ?? "Nerasporedjen",
                    item.StoreName ?? "Sve lokacije",
                    ResolveStockState(item.Quantity, item.Minimum),
                    item.AbcClass,
                    item.AgingLabel,
                    FormatInteger(item.Quantity),
                    FormatInteger(item.Minimum),
                    FormatInteger(reorderGap),
                    FormatCurrency(item.UnitCost),
                    FormatCurrency(item.EstimatedValue),
                    FormatInteger(item.DaysSinceMovement),
                    item.LastMovementAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", SerbianCulture) ?? "Nema kretanja"
                };
            }).ToList(),
            Filters =
            [
                new() { Key = "search", Label = "Pretraga", Value = string.IsNullOrWhiteSpace(dto.Search) ? "Sve" : dto.Search },
                new() { Key = "store", Label = "Prodavnica", Value = dto.StoreId?.ToString(SerbianCulture) ?? "Sve" },
                new() { Key = "supplier", Label = "Dobavljac", Value = dto.SupplierId?.ToString(SerbianCulture) ?? "Svi" },
                new() { Key = "sortBy", Label = "Sortiranje", Value = dto.SortBy switch
                    {
                        "naziv" => "Naziv A-Z",
                        "vrednost" => "Vrednost opadajuce",
                        "azuriranje" => "Poslednje azuriranje",
                        _ => "Kolicina opadajuce"
                    }
                }
            ],
            Metadata =
            [
                new() { Key = "generatedAt", Label = "Generisano", Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm", SerbianCulture) },
                new() { Key = "totalRows", Label = "Ukupno artikala", Value = items.Count.ToString(SerbianCulture) },
                new() { Key = "inventoryValue", Label = "Procena vrednosti", Value = FormatCurrency(insights.TotalEstimatedValue) },
                new() { Key = "aging90", Label = "Aging 90+", Value = insights.Aging.FirstOrDefault(x => x.BucketKey == "90+")?.ItemCount.ToString(SerbianCulture) ?? "0" },
                new() { Key = "classA", Label = "ABC klasa A", Value = insights.Abc.FirstOrDefault(x => x.BucketKey == "A")?.ItemCount.ToString(SerbianCulture) ?? "0" }
            ]
        };

        return new DocumentGenerationRequest
        {
            Format = preview ? "html" : (dto.Format ?? "pdf").ToLowerInvariant(),
            Orientation = string.IsNullOrWhiteSpace(dto.Orientation) ? "landscape" : dto.Orientation.ToLowerInvariant(),
            IncludeFiltersAndMetadata = dto.IncludeFiltersAndMetadata,
            Preview = preview,
            ForceAsync = preview ? false : dto.ForceAsync,
            Locale = "sr-RS",
            TemplateName = "analytics-table-default",
            DocumentType = "analytics-table-report",
            Table = table
        };
    }

    private static InventoryInsightsDto BuildInsights(List<InventoryDatasetItem> items) =>
        BuildInsights(items, new Dictionary<int, int>(), new Dictionary<int, InventorySignalWindowStats>());

    private static DocumentGenerationRequest BuildBlankDocumentRequest(string? orientation = null)
    {
        var resolvedOrientation = string.IsNullOrWhiteSpace(orientation) ? "landscape" : orientation.Trim().ToLowerInvariant();
        var columns = new List<DocumentColumnDefinition>
        {
            new() { Key = "plu",               Header = "PLU",                DataType = "text" },
            new() { Key = "naziv",             Header = "Naziv",              DataType = "text" },
            new() { Key = "supplier",          Header = "Dobavljac",          DataType = "text" },
            new() { Key = "store",             Header = "Prodavnica",         DataType = "text" },
            new() { Key = "stock",             Header = "Status",             DataType = "text" },
            new() { Key = "abc",               Header = "ABC",                DataType = "text" },
            new() { Key = "aging",             Header = "Aging",              DataType = "text" },
            new() { Key = "quantity",          Header = "Kolicina",           DataType = "number" },
            new() { Key = "minimum",           Header = "Minimum",            DataType = "number" },
            new() { Key = "gap",               Header = "Gap",                DataType = "number" },
            new() { Key = "unitCost",          Header = "Nabavna cena",       DataType = "currency" },
            new() { Key = "estimatedValue",    Header = "Vrednost zalihe",    DataType = "currency" },
            new() { Key = "daysSinceMovement", Header = "Dana bez kretanja",  DataType = "number" },
            new() { Key = "lastMovementAt",    Header = "Poslednje kretanje", DataType = "text" },
        };

        var emptyRows = Enumerable.Range(0, 35)
            .Select(_ => columns.Select(_ => (string?)null).ToList())
            .ToList();

        return new DocumentGenerationRequest
        {
            Format = "html",
            Orientation = resolvedOrientation,
            IncludeFiltersAndMetadata = false,
            Preview = true,
            ForceAsync = false,
            Locale = "sr-RS",
            TemplateName = "analytics-table-default",
            DocumentType = "inventory-balance-blank",
            Table = new DocumentTablePayload
            {
                TableKey = "inventory-balance-blank",
                TableTitle = "Bilans stanja — Prazan obrazac",
                Columns = columns,
                Filters = [],
                Metadata =
                [
                    new() { Key = "datum", Label = "Datum", Value = DateTime.Now.ToString("dd.MM.yyyy", SerbianCulture) }
                ],
                Rows = emptyRows
            }
        };
    }

    private static DocumentOperationResponseDto ToDocumentResponse(
        DocumentGenerateResult result,
        IDocumentDownloadTokenService tokenService,
        DocumentExportOptions options)
    {
        return new DocumentOperationResponseDto
        {
            DocumentId = result.DocumentId,
            BatchId = result.BatchId,
            Status = result.Status,
            IsAsync = result.IsAsync,
            FileName = result.FileName,
            MimeType = result.MimeType,
            SizeBytes = result.SizeBytes,
            CreatedAtUtc = result.CreatedAtUtc,
            CompletedAtUtc = result.CompletedAtUtc,
            ExpiresAtUtc = result.ExpiresAtUtc,
            StatusUrl = $"/api/exports/{result.DocumentId}/status",
            DownloadUrl = result.CompletedAtUtc.HasValue
                ? $"/api/documents/{result.DocumentId}?token={tokenService.Create(result.DocumentId, DateTime.UtcNow.AddMinutes(options.SignedUrlTtlMinutes))}"
                : null,
            PrintUrl = $"/api/documents/{result.DocumentId}/print?token={tokenService.Create(result.DocumentId, DateTime.UtcNow.AddMinutes(options.SignedUrlTtlMinutes))}"
        };
    }

    private static string FormatInteger(int value) => value.ToString("N0", SerbianCulture);

    private static string FormatCurrency(decimal value) =>
        value.ToString("C0", SerbianCulture);

    private sealed record InventoryArticleProjection(
        int Id,
        string? Plu,
        string Naziv,
        int Quantity,
        int Minimum,
        decimal UnitCost,
        int? StoreId,
        int? SupplierId,
        string? Kategorija,
        string? Pol,
        string? Materijal,
        DateTime UpdatedAt);

    private sealed record InventoryHistoryProjection(
        int MovementId,
        string TipPromene,
        DateTime Datum,
        int? Kolicina,
        decimal Iznos,
        string? BrojDokumenta,
        string? KorisnikIme,
        string? DataOrigin,
        int? StoreId,
        int? SupplierId,
        decimal? StaraCena,
        decimal? NovaCena,
        string? Komentar);

    private sealed record InventoryMovementStats(
        DateTime LastMovementAt,
        int MovementCount30d);

    private sealed record InventorySignalWindowStats(
        int NetMovementUnits,
        int InboundUnits);

    private sealed record InventoryDatasetItem(
        int Id,
        string? Plu,
        string Naziv,
        int Quantity,
        int Minimum,
        decimal UnitCost,
        decimal EstimatedValue,
        int? StoreId,
        string? StoreName,
        int? SupplierId,
        string? SupplierName,
        string? Kategorija,
        string? Pol,
        string? Materijal,
        DateTime UpdatedAtUtc,
        DateTime? LastMovementAtUtc,
        int MovementCount30d,
        int DaysSinceMovement,
        string AgingBucket,
        string AgingLabel,
        string AbcClass);
}
