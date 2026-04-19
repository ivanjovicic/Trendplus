using System.Diagnostics;
using Application.Analytics;
using Domain.Model.Analytics;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Services;

public sealed class AnalyticsCostSnapshotService
{
    private const string SupportedScope = "access_origin";
    private const int InsertBatchSize = 5000;

    private readonly TrendplusDbContext _db;
    private readonly ILogger<AnalyticsCostSnapshotService> _logger;
    private readonly IOptionsMonitor<AnalyticsSnapshotOptions> _snapshotOptions;

    public AnalyticsCostSnapshotService(
        TrendplusDbContext db,
        ILogger<AnalyticsCostSnapshotService> logger,
        IOptionsMonitor<AnalyticsSnapshotOptions> snapshotOptions)
    {
        _db = db;
        _logger = logger;
        _snapshotOptions = snapshotOptions;
    }

    // ── Create ──────────────────────────────────────────────────────────

    public async Task<AnalyticsCostSnapshotBatch> CreateBatchAsync(
        string? description, string createdBy, CancellationToken ct)
    {
        var batch = new AnalyticsCostSnapshotBatch
        {
            Scope = SupportedScope,
            Status = "draft",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy.Trim(),
            Description = description?.Trim(),
        };

        _db.AnalyticsCostSnapshotBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Snapshot batch {BatchId} created (scope={Scope}, createdBy={CreatedBy})",
            batch.Id, batch.Scope, batch.CreatedBy);

        return batch;
    }

    // ── Generate ────────────────────────────────────────────────────────

    public async Task<AnalyticsCostSnapshotBatch> GenerateBatchAsync(
        long batchId, bool dryRun, CancellationToken ct)
    {
        var batch = await _db.AnalyticsCostSnapshotBatches
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");

        if (batch.Status is not ("draft" or "failed" or "generating"))
            throw new InvalidOperationException(
                $"Batch {batchId} is in status '{batch.Status}'; only 'draft', 'failed', or 'generating' batches can be generated.");

        batch.Status = "generating";
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Snapshot batch {BatchId} generation started (scope={Scope}, dryRun={DryRun})",
            batchId, batch.Scope, dryRun);

        var sw = Stopwatch.StartNew();

        try
        {
            // Clear any previous rows if retrying a failed batch
            var existingRows = await _db.AnalyticsSaleLineCostSnapshots
                .Where(s => s.BatchId == batchId)
                .CountAsync(ct);

            if (existingRows > 0)
            {
                await _db.AnalyticsSaleLineCostSnapshots
                    .Where(s => s.BatchId == batchId)
                    .ExecuteDeleteAsync(ct);
            }

            // ── Main projection query ──
            // Joins ProdajaStavke → ProdajaZaglavlje → Artikli in one query.
            // Only Access-origin lines with NULL nabavna_cena.
            var eligibleLines = await _db.ProdajaStavke
                .Join(
                    _db.ProdajaZaglavlja,
                    ps => ps.IdProdaja,
                    pz => pz.Id,
                    (ps, pz) => new { ps, pz })
                .Where(x => x.pz.DataOrigin == "access" && x.ps.NabavnaCena == null)
                .Join(
                    _db.Artikli,
                    x => x.ps.IdArtikal,
                    a => a.Id,
                    (x, a) => new
                    {
                        ProdajaStavkaId = x.ps.Id,
                        ArtikalId = a.Id,
                        x.ps.Kolicina,
                        x.ps.Cena,
                        a.NabavnaCenaDin,
                        a.NabavnaCena,
                    })
                .AsNoTracking()
                .ToListAsync(ct);

            // ── Resolve cost and build snapshot rows ──
            int resolvedCount = 0;
            int noCostCount = 0;
            decimal totalRevenueCovered = 0m;
            decimal totalRevenueNoCost = 0m;
            decimal totalRevenueAll = 0m;
            var snapshotRows = new List<AnalyticsSaleLineCostSnapshot>(eligibleLines.Count);

            foreach (var line in eligibleLines)
            {
                decimal lineRevenue = line.Kolicina * line.Cena;
                totalRevenueAll += lineRevenue;

                decimal resolvedCost;
                short costSource;

                if (line.NabavnaCenaDin is > 0)
                {
                    resolvedCost = line.NabavnaCenaDin.Value;
                    costSource = (short)MarginCostSource.ProductFallbackRsd;
                }
                else if (line.NabavnaCena is > 0)
                {
                    resolvedCost = line.NabavnaCena.Value;
                    costSource = (short)MarginCostSource.ProductFallbackLegacy;
                }
                else
                {
                    noCostCount++;
                    totalRevenueNoCost += lineRevenue;
                    continue; // No snapshot row for unresolved lines
                }

                resolvedCount++;
                totalRevenueCovered += lineRevenue;

                snapshotRows.Add(new AnalyticsSaleLineCostSnapshot
                {
                    BatchId = batchId,
                    ProdajaStavkaId = line.ProdajaStavkaId,
                    ResolvedUnitCost = resolvedCost,
                    CostSource = costSource,
                    ProductCostRsdAtSnapshot = line.NabavnaCenaDin,
                    ProductCostLegacyAtSnapshot = line.NabavnaCena,
                    ArtikalId = line.ArtikalId,
                });
            }

            // ── Batched insert ──
            for (int i = 0; i < snapshotRows.Count; i += InsertBatchSize)
            {
                var chunk = snapshotRows.GetRange(i, Math.Min(InsertBatchSize, snapshotRows.Count - i));
                _db.AnalyticsSaleLineCostSnapshots.AddRange(chunk);
                await _db.SaveChangesAsync(ct);

                // Detach tracked entities to reduce memory pressure
                foreach (var entity in chunk)
                    _db.Entry(entity).State = EntityState.Detached;
            }

            sw.Stop();

            // ── Update batch metadata ──
            batch.RowCount = resolvedCount;
            batch.TotalRevenueCovered = totalRevenueCovered;
            batch.CoveragePct = totalRevenueAll > 0
                ? (double)(totalRevenueCovered / totalRevenueAll) * 100.0
                : 0;
            batch.NoCostPct = totalRevenueAll > 0
                ? (double)(totalRevenueNoCost / totalRevenueAll) * 100.0
                : 0;
            batch.GenerationDurationMs = (int)sw.ElapsedMilliseconds;
            batch.GeneratedAtUtc = DateTime.UtcNow;
            batch.DryRun = dryRun;
            batch.Status = "ready";
            batch.ErrorMessage = null;

            await _db.SaveChangesAsync(ct);

            var remainingLiveFallbackPct = ComputeRemainingLiveFallbackPct(batch.CoveragePct, batch.NoCostPct);

            _logger.LogInformation(
                "Snapshot batch {BatchId} generation completed: {RowCount} rows, " +
                "{CoveragePct:F1}% coverage, {RemainingLiveFallbackPct:F1}% remaining-live-fallback, {NoCostPct:F1}% no-cost, {DurationMs}ms (dryRun={DryRun})",
                batchId, resolvedCount, batch.CoveragePct, remainingLiveFallbackPct,
                batch.NoCostPct, sw.ElapsedMilliseconds, dryRun);

            return batch;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            batch.Status = "failed";
            batch.ErrorMessage = ex.Message.Length > 2000
                ? ex.Message[..2000]
                : ex.Message;
            batch.GenerationDurationMs = (int)sw.ElapsedMilliseconds;

            try
            {
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "Failed to persist failure status for batch {BatchId}", batchId);
            }

            _logger.LogError(ex,
                "Snapshot batch {BatchId} generation failed after {DurationMs}ms",
                batchId, sw.ElapsedMilliseconds);

            throw;
        }
    }

    // ── Activate ────────────────────────────────────────────────────────

    public async Task<AnalyticsCostSnapshotBatch> ActivateBatchAsync(
        long batchId, CancellationToken ct)
    {
        var batch = await _db.AnalyticsCostSnapshotBatches
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");

        if (batch.Status != "ready")
            throw new InvalidOperationException(
                $"Batch {batchId} is in status '{batch.Status}'; only 'ready' batches can be activated.");

        if (batch.DryRun)
            throw new InvalidOperationException(
                $"Batch {batchId} is a dry-run batch and cannot be activated.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Supersede any currently-active batch for this scope
        var activeBatch = await _db.AnalyticsCostSnapshotBatches
            .FirstOrDefaultAsync(
                b => b.Scope == batch.Scope && b.Status == "active", ct);

        if (activeBatch is not null)
        {
            activeBatch.Status = "superseded";
            activeBatch.DeactivatedAtUtc = DateTime.UtcNow;

            _logger.LogInformation(
                "Snapshot batch {OldBatchId} superseded by batch {NewBatchId} (scope={Scope})",
                activeBatch.Id, batchId, batch.Scope);
        }

        batch.Status = "active";
        batch.ActivatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Snapshot batch {BatchId} activated (scope={Scope}, rowCount={RowCount}, coverage={CoveragePct:F1}%)",
            batchId, batch.Scope, batch.RowCount, batch.CoveragePct);

        return batch;
    }

    // ── Deactivate ──────────────────────────────────────────────────────

    public async Task<AnalyticsCostSnapshotBatch> DeactivateBatchAsync(
        long batchId, CancellationToken ct)
    {
        var batch = await _db.AnalyticsCostSnapshotBatches
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");

        if (batch.Status != "active")
            throw new InvalidOperationException(
                $"Batch {batchId} is in status '{batch.Status}'; only 'active' batches can be deactivated.");

        batch.Status = "deactivated";
        batch.DeactivatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Snapshot batch {BatchId} deactivated (scope={Scope})",
            batchId, batch.Scope);

        return batch;
    }

    // ── List ────────────────────────────────────────────────────────────

    public async Task<List<AnalyticsCostSnapshotBatch>> ListBatchesAsync(
        string? scope, CancellationToken ct)
    {
        var query = _db.AnalyticsCostSnapshotBatches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(scope))
            query = query.Where(b => b.Scope == scope);

        return await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(ct);
    }

    // ── Detail ──────────────────────────────────────────────────────────

    public async Task<BatchDetailResult?> GetBatchDetailAsync(
        long batchId, CancellationToken ct)
    {
        var batch = await _db.AnalyticsCostSnapshotBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

        if (batch is null)
            return null;

        // Source breakdown: count rows per cost_source
        var sourceBreakdown = await _db.AnalyticsSaleLineCostSnapshots
            .Where(s => s.BatchId == batchId)
            .GroupBy(s => s.CostSource)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync(ct);

        var breakdown = new Dictionary<string, int>();
        foreach (var item in sourceBreakdown)
        {
            var label = item.Source switch
            {
                (short)MarginCostSource.ProductFallbackRsd => "productFallbackRsd",
                (short)MarginCostSource.ProductFallbackLegacy => "productFallbackLegacy",
                _ => $"unknown_{item.Source}",
            };
            breakdown[label] = item.Count;
        }

        return new BatchDetailResult(batch, breakdown);
    }

    // ── Health ───────────────────────────────────────────────────────────

    public async Task<SnapshotHealthResult> GetHealthAsync(CancellationToken ct)
    {
        var options = _snapshotOptions.CurrentValue;

        var activeBatch = await _db.AnalyticsCostSnapshotBatches
            .AsNoTracking()
            .Where(b => b.Status == "active" && b.Scope == SupportedScope)
            .Select(b => new SnapshotBatchProjection(
                b.Id,
                b.Scope,
                b.Status,
                b.DryRun,
                b.CreatedAtUtc,
                b.GeneratedAtUtc,
                b.ActivatedAtUtc,
                b.RowCount,
                b.CoveragePct,
                b.NoCostPct,
                b.GenerationDurationMs))
            .FirstOrDefaultAsync(ct);

        var latestBatch = await _db.AnalyticsCostSnapshotBatches
            .AsNoTracking()
            .Where(b => b.Scope == SupportedScope)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new SnapshotBatchProjection(
                b.Id,
                b.Scope,
                b.Status,
                b.DryRun,
                b.CreatedAtUtc,
                b.GeneratedAtUtc,
                b.ActivatedAtUtc,
                b.RowCount,
                b.CoveragePct,
                b.NoCostPct,
                b.GenerationDurationMs))
            .FirstOrDefaultAsync(ct);

        double? ageHours = null;
        bool isStale = false;
        double? remainingLiveFallbackPct = null;

        if (activeBatch is not null)
        {
            var referenceUtc = activeBatch.GeneratedAtUtc ?? activeBatch.ActivatedAtUtc ?? activeBatch.CreatedAtUtc;
            ageHours = Math.Round((DateTime.UtcNow - referenceUtc).TotalHours, 1);
            isStale = ageHours >= options.ActiveBatchStaleAfterHours;
            remainingLiveFallbackPct = ComputeRemainingLiveFallbackPct(activeBatch.CoveragePct, activeBatch.NoCostPct);
        }

        string? warning = null;
        if (options.UseSnapshotCost && activeBatch is null)
        {
            warning = "UseSnapshotCost je ukljucen, ali aktivni snapshot batch ne postoji. Read path je na legacy fallback-u.";
        }
        else if (isStale && ageHours is not null)
        {
            warning = $"Aktivni snapshot batch je star {ageHours:0.#}h i premasio je prag od {options.ActiveBatchStaleAfterHours}h.";
        }

        return new SnapshotHealthResult(
            FeatureFlagEnabled: options.UseSnapshotCost,
            AdminEnabled: options.SnapshotAdminEnabled,
            HasActiveBatch: activeBatch is not null,
            ActiveBatchId: activeBatch?.Id,
            ActiveBatchStatus: activeBatch?.Status,
            ActiveBatchDryRun: activeBatch?.DryRun,
            Scope: activeBatch?.Scope,
            GeneratedAtUtc: activeBatch?.GeneratedAtUtc,
            ActivatedAtUtc: activeBatch?.ActivatedAtUtc,
            RowCount: activeBatch?.RowCount,
            CoveragePct: activeBatch?.CoveragePct,
            NoCostPct: activeBatch?.NoCostPct,
            RemainingLiveFallbackPct: remainingLiveFallbackPct,
            GenerationDurationMs: activeBatch?.GenerationDurationMs,
            AgeHours: ageHours,
            StaleAfterHours: options.ActiveBatchStaleAfterHours,
            IsStale: isStale,
            Warning: warning,
            LatestBatchId: latestBatch?.Id,
            LatestBatchStatus: latestBatch?.Status,
            LatestBatchDryRun: latestBatch?.DryRun,
            LatestBatchGeneratedAtUtc: latestBatch?.GeneratedAtUtc);
    }

    // ── Reconciliation ─────────────────────────────────────────────────────

    public async Task<SnapshotAnalyticsComparisonResult> CompareSupplierAnalyticsAsync(
        SnapshotAnalyticsComparisonRequest request,
        CancellationToken ct)
    {
        var context = await ResolveComparisonContextAsync(request, ct);

        var supplierNames = await _db.Dobavljaci.AsNoTracking()
            .Select(d => new { d.Id, d.Naziv })
            .ToDictionaryAsync(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(x.Naziv) ? "Nepoznato" : x.Naziv.Trim(),
                ct);

        var salesLines = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            where (!context.Filters.FromDateUtc.HasValue || pz.DatumProdaje >= context.Filters.FromDateUtc.Value)
               && (!context.Filters.ToDateUtc.HasValue || pz.DatumProdaje <= context.Filters.ToDateUtc.Value)
               && (!context.Filters.StoreId.HasValue || pz.IDObjekat == context.Filters.StoreId.Value)
               && (!context.Filters.ImportedOnly || a.DataOrigin == "access")
               && (!context.Filters.ExistingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
            group new
            {
                ps.Kolicina,
                Revenue = ps.Kolicina * ps.Cena,
                SaleLineCost = ps.NabavnaCena,
                ProductCostRsd = a.NabavnaCenaDin,
                ProductCostLegacy = a.NabavnaCena
            } by new
            {
                SupplierId = a.IDDobavljac,
                ArtikalId = a.Id,
                SaleLineCost = ps.NabavnaCena,
                ProductCostRsd = a.NabavnaCenaDin,
                ProductCostLegacy = a.NabavnaCena
            }
            into g
            select new SupplierComparisonLine(
                g.Key.SupplierId,
                g.Key.ArtikalId,
                g.Sum(x => x.Kolicina),
                g.Sum(x => x.Revenue),
                g.Key.SaleLineCost,
                g.Key.ProductCostRsd,
                g.Key.ProductCostLegacy))
            .ToListAsync(ct);

        string ResolveSupplierName(int? supplierId)
        {
            if (!supplierId.HasValue)
            {
                return "Nepoznato";
            }

            return supplierNames.TryGetValue(supplierId.Value, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "Nepoznato";
        }

        var metrics = BuildComparisonMetrics(
            salesLines,
            line => BuildEntityBucketKey(line.SupplierId),
            line => line.SupplierId,
            line => ResolveSupplierName(line.SupplierId),
            line => line.ArtikalId,
            line => line.Quantity,
            line => line.Revenue,
            line => line.SaleLineCost,
            line => line.ProductCostRsd,
            line => line.ProductCostLegacy,
            context.SnapshotCostByArtikalId);

        var result = BuildComparisonResult(
            reportKey: "supplier-sales-stats",
            context,
            metrics);

        _logger.LogInformation(
            "Snapshot reconciliation computed for {ReportKey}. BatchId={BatchId} FeatureFlagEnabled={FeatureFlagEnabled} EntityCount={EntityCount} ChangedEntityCount={ChangedEntityCount} MarginDelta={MarginDelta} CoverageDeltaPct={CoverageDeltaPct:F2}",
            result.ReportKey,
            result.Batch.BatchId,
            result.FeatureFlagEnabled,
            result.EntityCount,
            result.ChangedEntityCount,
            result.Delta.MarginContribution,
            result.Delta.CoveragePct);

        return result;
    }

    public async Task<SnapshotAnalyticsComparisonResult> CompareShoeTypeAnalyticsAsync(
        SnapshotAnalyticsComparisonRequest request,
        CancellationToken ct)
    {
        var context = await ResolveComparisonContextAsync(request, ct);

        var shoeTypeNames = await _db.TipoviObuce.AsNoTracking()
            .Select(t => new { t.Id, t.Naziv })
            .ToDictionaryAsync(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(x.Naziv) ? "Nepoznato" : x.Naziv.Trim(),
                ct);

        var salesLines = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            where (!context.Filters.FromDateUtc.HasValue || pz.DatumProdaje >= context.Filters.FromDateUtc.Value)
               && (!context.Filters.ToDateUtc.HasValue || pz.DatumProdaje <= context.Filters.ToDateUtc.Value)
               && (!context.Filters.StoreId.HasValue || pz.IDObjekat == context.Filters.StoreId.Value)
               && (!context.Filters.ImportedOnly || a.DataOrigin == "access")
               && (!context.Filters.ExistingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
            group new
            {
                ps.Kolicina,
                Revenue = ps.Kolicina * ps.Cena,
                SaleLineCost = ps.NabavnaCena,
                ProductCostRsd = a.NabavnaCenaDin,
                ProductCostLegacy = a.NabavnaCena
            } by new
            {
                ShoeTypeId = a.IDTipObuce,
                ArtikalId = a.Id,
                SaleLineCost = ps.NabavnaCena,
                ProductCostRsd = a.NabavnaCenaDin,
                ProductCostLegacy = a.NabavnaCena
            }
            into g
            select new ShoeTypeComparisonLine(
                g.Key.ShoeTypeId,
                g.Key.ArtikalId,
                g.Sum(x => x.Kolicina),
                g.Sum(x => x.Revenue),
                g.Key.SaleLineCost,
                g.Key.ProductCostRsd,
                g.Key.ProductCostLegacy))
            .ToListAsync(ct);

        string ResolveShoeTypeName(int? shoeTypeId)
        {
            if (!shoeTypeId.HasValue)
            {
                return "Nepoznato";
            }

            return shoeTypeNames.TryGetValue(shoeTypeId.Value, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "Nepoznato";
        }

        var metrics = BuildComparisonMetrics(
            salesLines,
            line => BuildEntityBucketKey(line.ShoeTypeId),
            line => line.ShoeTypeId,
            line => ResolveShoeTypeName(line.ShoeTypeId),
            line => line.ArtikalId,
            line => line.Quantity,
            line => line.Revenue,
            line => line.SaleLineCost,
            line => line.ProductCostRsd,
            line => line.ProductCostLegacy,
            context.SnapshotCostByArtikalId);

        var result = BuildComparisonResult(
            reportKey: "shoe-type-sales-stats",
            context,
            metrics);

        _logger.LogInformation(
            "Snapshot reconciliation computed for {ReportKey}. BatchId={BatchId} FeatureFlagEnabled={FeatureFlagEnabled} EntityCount={EntityCount} ChangedEntityCount={ChangedEntityCount} MarginDelta={MarginDelta} CoverageDeltaPct={CoverageDeltaPct:F2}",
            result.ReportKey,
            result.Batch.BatchId,
            result.FeatureFlagEnabled,
            result.EntityCount,
            result.ChangedEntityCount,
            result.Delta.MarginContribution,
            result.Delta.CoveragePct);

        return result;
    }

    private async Task<ComparisonContext> ResolveComparisonContextAsync(
        SnapshotAnalyticsComparisonRequest request,
        CancellationToken ct)
    {
        var filters = await ResolveComparisonFiltersAsync(request, ct);
        var batch = await ResolveComparisonBatchAsync(request.BatchId, ct)
            ?? throw new InvalidOperationException(
                "Aktivni snapshot batch ne postoji. Prosledite batchId za eksplicitno poredjenje ili aktivirajte batch.");

        if (batch.Status is "draft" or "generating" or "failed")
        {
            throw new InvalidOperationException(
                $"Batch {batch.Id} je u statusu '{batch.Status}' i nije spreman za poredjenje.");
        }

        var snapshotCostByArtikalId = await _db.AnalyticsSaleLineCostSnapshots
            .Where(s => s.BatchId == batch.Id)
            .GroupBy(s => s.ArtikalId)
            .Select(g => new { ArtikalId = g.Key, Cost = g.Min(s => s.ResolvedUnitCost) })
            .ToDictionaryAsync(x => x.ArtikalId, x => x.Cost, ct);

        return new ComparisonContext(
            Filters: filters,
            Batch: new SnapshotComparisonBatch(
                batch.Id,
                batch.Status,
                batch.DryRun,
                request.BatchId.HasValue,
                batch.GeneratedAtUtc,
                batch.ActivatedAtUtc),
            FeatureFlagEnabled: _snapshotOptions.CurrentValue.UseSnapshotCost,
            SnapshotCostByArtikalId: snapshotCostByArtikalId,
            Top: Math.Clamp(request.Top ?? 25, 1, 100));
    }

    private async Task<ComparisonFilters> ResolveComparisonFiltersAsync(
        SnapshotAnalyticsComparisonRequest request,
        CancellationToken ct)
    {
        var fromUtc = NormalizeUtc(request.FromDate);
        var toUtc = NormalizeUtc(request.ToDate);
        var normalizedDataScope = NormalizeDataScope(request.DataScope);

        if (request.SezonaId.HasValue)
        {
            var sezona = await _db.Sezone.AsNoTracking()
                .Where(s => s.Id == request.SezonaId.Value)
                .Select(s => new { s.DatumOd, s.DatumDo })
                .FirstOrDefaultAsync(ct);

            if (sezona is null)
            {
                throw new InvalidOperationException($"Sezona {request.SezonaId.Value} nije pronadjena.");
            }

            fromUtc = DateTime.SpecifyKind(sezona.DatumOd.Date, DateTimeKind.Utc);
            toUtc = DateTime.SpecifyKind(sezona.DatumDo.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        }

        if (!fromUtc.HasValue && !toUtc.HasValue)
        {
            var todayUtc = DateTime.UtcNow.Date;
            fromUtc = todayUtc.AddDays(-89);
            toUtc = todayUtc.AddDays(1).AddTicks(-1);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
        {
            throw new InvalidOperationException("Neispravan period: fromDate mora biti manji ili jednak toDate.");
        }

        return new ComparisonFilters(
            fromUtc,
            toUtc,
            request.SezonaId,
            request.StoreId,
            normalizedDataScope,
            ImportedOnly: normalizedDataScope == "imported",
            ExistingOnly: normalizedDataScope == "existing");
    }

    private async Task<AnalyticsCostSnapshotBatch?> ResolveComparisonBatchAsync(long? batchId, CancellationToken ct)
    {
        if (batchId.HasValue)
        {
            return await _db.AnalyticsCostSnapshotBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId.Value && b.Scope == SupportedScope, ct)
                ?? throw new InvalidOperationException($"Snapshot batch {batchId.Value} nije pronađen.");
        }

        return await _db.AnalyticsCostSnapshotBatches
            .AsNoTracking()
            .Where(b => b.Status == "active" && b.Scope == SupportedScope)
            .FirstOrDefaultAsync(ct);
    }

    private static List<ComparisonEntityMetric> BuildComparisonMetrics<TLine>(
        IEnumerable<TLine> lines,
        Func<TLine, string> bucketKeySelector,
        Func<TLine, int?> entityIdSelector,
        Func<TLine, string> entityNameSelector,
        Func<TLine, int> artikalIdSelector,
        Func<TLine, int> quantitySelector,
        Func<TLine, decimal> revenueSelector,
        Func<TLine, decimal?> saleLineCostSelector,
        Func<TLine, decimal?> productCostRsdSelector,
        Func<TLine, decimal?> productCostLegacySelector,
        IReadOnlyDictionary<int, decimal> snapshotCostByArtikalId)
    {
        return lines
            .GroupBy(bucketKeySelector)
            .Select(g =>
            {
                var first = g.First();
                decimal totalRevenue = 0m;
                var legacy = new MarginAccumulator();
                var snapshot = new MarginAccumulator();

                foreach (var line in g)
                {
                    var revenue = revenueSelector(line);
                    var quantity = quantitySelector(line);
                    var saleLineCost = saleLineCostSelector(line);
                    var productCostRsd = productCostRsdSelector(line);
                    var productCostLegacy = productCostLegacySelector(line);
                    decimal? snapshotCost = null;

                    if (saleLineCost is null && snapshotCostByArtikalId.TryGetValue(artikalIdSelector(line), out var resolvedSnapshotCost))
                    {
                        snapshotCost = resolvedSnapshotCost;
                    }

                    totalRevenue += revenue;
                    legacy.Add(revenue, quantity, saleLineCost, productCostRsd, productCostLegacy);
                    snapshot.Add(revenue, quantity, saleLineCost, snapshotCost, productCostRsd, productCostLegacy);
                }

                return new ComparisonEntityMetric(
                    BucketKey: g.Key,
                    EntityId: entityIdSelector(first),
                    EntityName: entityNameSelector(first),
                    Revenue: Math.Round(totalRevenue, 2),
                    Legacy: legacy.Build(totalRevenue),
                    Snapshot: snapshot.Build(totalRevenue));
            })
            .OrderBy(x => x.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SnapshotAnalyticsComparisonResult BuildComparisonResult(
        string reportKey,
        ComparisonContext context,
        IReadOnlyList<ComparisonEntityMetric> metrics)
    {
        var legacyRankMap = BuildMarginContributionRankMap(metrics, metric => metric.Legacy.MarginContribution);
        var snapshotRankMap = BuildMarginContributionRankMap(metrics, metric => metric.Snapshot.MarginContribution);

        var totalRevenue = metrics.Sum(metric => metric.Revenue);
        var legacyTotals = BuildComparisonTotalsFromMetrics(metrics, totalRevenue, useSnapshotCoverage: false);
        var snapshotTotals = BuildComparisonTotalsFromMetrics(metrics, totalRevenue, useSnapshotCoverage: true);

        var allDeltas = metrics
            .Select(metric =>
            {
                var legacyCoverage = Math.Round(metric.Legacy.MarginDataCoveragePct ?? 0d, 2);
                var snapshotCoverage = Math.Round(metric.Snapshot.MarginDataCoveragePct ?? 0d, 2);
                var legacyRank = legacyRankMap[metric.BucketKey];
                var snapshotRank = snapshotRankMap[metric.BucketKey];
                return new SnapshotAnalyticsEntityDelta(
                    metric.EntityId,
                    metric.EntityName,
                    metric.Revenue,
                    metric.Legacy.MarginContribution,
                    metric.Snapshot.MarginContribution,
                    Math.Round(metric.Snapshot.MarginContribution - metric.Legacy.MarginContribution, 2),
                    metric.Legacy.MarginPct,
                    metric.Snapshot.MarginPct,
                    Math.Round(metric.Snapshot.MarginPct - metric.Legacy.MarginPct, 2),
                    legacyCoverage,
                    snapshotCoverage,
                    Math.Round(snapshotCoverage - legacyCoverage, 2),
                    legacyRank,
                    snapshotRank,
                    legacyRank - snapshotRank);
            })
            .ToList();

        var changedEntityCount = allDeltas.Count(delta =>
            Math.Abs(delta.MarginContributionDelta) > 0.01m ||
            Math.Abs(delta.MarginPctDelta) > 0.01d ||
            Math.Abs(delta.CoveragePctDelta) > 0.01d ||
            delta.MarginContributionRankDelta != 0);

        var largestDeltas = allDeltas
            .OrderByDescending(delta => Math.Abs(delta.MarginContributionDelta))
            .ThenByDescending(delta => Math.Abs(delta.CoveragePctDelta))
            .ThenByDescending(delta => Math.Abs(delta.MarginContributionRankDelta))
            .Take(context.Top)
            .ToList();

        return new SnapshotAnalyticsComparisonResult(
            ReportKey: reportKey,
            Batch: context.Batch,
            Filters: new SnapshotAnalyticsComparisonFilters(
                context.Filters.FromDateUtc,
                context.Filters.ToDateUtc,
                context.Filters.SezonaId,
                context.Filters.StoreId,
                context.Filters.DataScope),
            FeatureFlagEnabled: context.FeatureFlagEnabled,
            EntityCount: metrics.Count,
            ChangedEntityCount: changedEntityCount,
            Legacy: legacyTotals,
            Snapshot: snapshotTotals,
            Delta: new SnapshotAnalyticsComparisonTotals(
                MarginContribution: Math.Round(snapshotTotals.MarginContribution - legacyTotals.MarginContribution, 2),
                MarginPct: Math.Round(snapshotTotals.MarginPct - legacyTotals.MarginPct, 2),
                CoveragePct: Math.Round(snapshotTotals.CoveragePct - legacyTotals.CoveragePct, 2),
                NoCostPct: Math.Round(snapshotTotals.NoCostPct - legacyTotals.NoCostPct, 2),
                LiveFallbackPct: Math.Round(snapshotTotals.LiveFallbackPct - legacyTotals.LiveFallbackPct, 2),
                SnapshotCoveragePct: Math.Round(snapshotTotals.SnapshotCoveragePct - legacyTotals.SnapshotCoveragePct, 2)),
            LargestDeltas: largestDeltas);
    }

    private static SnapshotAnalyticsComparisonTotals BuildComparisonTotalsFromMetrics(
        IReadOnlyList<ComparisonEntityMetric> metrics,
        decimal totalRevenue,
        bool useSnapshotCoverage)
    {
        var marginContribution = Math.Round(metrics.Sum(metric => useSnapshotCoverage ? metric.Snapshot.MarginContribution : metric.Legacy.MarginContribution), 2);
        var revenueWithCost = metrics.Sum(metric => useSnapshotCoverage ? metric.Snapshot.RevenueWithCost : metric.Legacy.RevenueWithCost);
        var totalCost = metrics.Sum(metric => useSnapshotCoverage ? metric.Snapshot.TotalCost : metric.Legacy.TotalCost);
        var marginPct = revenueWithCost > 0m
            ? Math.Round((double)((revenueWithCost - totalCost) / revenueWithCost * 100m), 2)
            : 0d;
        var coveragePct = totalRevenue > 0m
            ? Math.Round((double)(revenueWithCost / totalRevenue * 100m), 2)
            : 0d;
        var snapshotCoveragePct = totalRevenue > 0m && useSnapshotCoverage
            ? Math.Round((double)(metrics.Sum(metric => metric.Snapshot.SnapshotCostRevenue) / totalRevenue * 100m), 2)
            : 0d;
        var liveFallbackPct = totalRevenue > 0m
            ? Math.Round((double)(metrics.Sum(metric => (useSnapshotCoverage ? metric.Snapshot : metric.Legacy).EstimatedCostRevenue) / totalRevenue * 100m), 2)
            : 0d;
        var noCostPct = totalRevenue > 0m
            ? Math.Round((double)((totalRevenue - revenueWithCost) / totalRevenue * 100m), 2)
            : 0d;

        return new SnapshotAnalyticsComparisonTotals(
            MarginContribution: marginContribution,
            MarginPct: marginPct,
            CoveragePct: coveragePct,
            NoCostPct: noCostPct,
            LiveFallbackPct: liveFallbackPct,
            SnapshotCoveragePct: snapshotCoveragePct);
    }

    private static Dictionary<string, int> BuildMarginContributionRankMap(
        IReadOnlyList<ComparisonEntityMetric> metrics,
        Func<ComparisonEntityMetric, decimal> selector)
    {
        return metrics
            .OrderByDescending(selector)
            .ThenBy(metric => metric.EntityName, StringComparer.OrdinalIgnoreCase)
            .Select((metric, index) => new { metric.BucketKey, Rank = index + 1 })
            .ToDictionary(x => x.BucketKey, x => x.Rank, StringComparer.Ordinal);
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var date = value.Value;
        return date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : date.ToUniversalTime();
    }

    private static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    private static string BuildEntityBucketKey(int? entityId)
        => entityId.HasValue ? $"id:{entityId.Value}" : "unknown";

    private static double ComputeRemainingLiveFallbackPct(double coveragePct, double noCostPct)
        => Math.Max(0d, Math.Round(100d - coveragePct - noCostPct, 2));

    // ── Result types ────────────────────────────────────────────────────

    public sealed record BatchDetailResult(
        AnalyticsCostSnapshotBatch Batch,
        Dictionary<string, int> CostSourceBreakdown);

    private sealed record SnapshotBatchProjection(
        long Id,
        string Scope,
        string Status,
        bool DryRun,
        DateTime CreatedAtUtc,
        DateTime? GeneratedAtUtc,
        DateTime? ActivatedAtUtc,
        int RowCount,
        double CoveragePct,
        double NoCostPct,
        int? GenerationDurationMs);

    private sealed record ComparisonFilters(
        DateTime? FromDateUtc,
        DateTime? ToDateUtc,
        int? SezonaId,
        int? StoreId,
        string DataScope,
        bool ImportedOnly,
        bool ExistingOnly);

    private sealed record ComparisonContext(
        ComparisonFilters Filters,
        SnapshotComparisonBatch Batch,
        bool FeatureFlagEnabled,
        IReadOnlyDictionary<int, decimal> SnapshotCostByArtikalId,
        int Top);

    private sealed record SupplierComparisonLine(
        int? SupplierId,
        int ArtikalId,
        int Quantity,
        decimal Revenue,
        decimal? SaleLineCost,
        decimal? ProductCostRsd,
        decimal? ProductCostLegacy);

    private sealed record ShoeTypeComparisonLine(
        int? ShoeTypeId,
        int ArtikalId,
        int Quantity,
        decimal Revenue,
        decimal? SaleLineCost,
        decimal? ProductCostRsd,
        decimal? ProductCostLegacy);

    private sealed record ComparisonEntityMetric(
        string BucketKey,
        int? EntityId,
        string EntityName,
        decimal Revenue,
        MarginSnapshot Legacy,
        MarginSnapshot Snapshot);

    public sealed record SnapshotAnalyticsComparisonRequest(
        long? BatchId,
        int? SezonaId,
        DateTime? FromDate,
        DateTime? ToDate,
        int? StoreId,
        string? DataScope,
        int? Top = null);

    public sealed record SnapshotComparisonBatch(
        long BatchId,
        string Status,
        bool DryRun,
        bool ExplicitBatchRequested,
        DateTime? GeneratedAtUtc,
        DateTime? ActivatedAtUtc);

    public sealed record SnapshotAnalyticsComparisonFilters(
        DateTime? FromDateUtc,
        DateTime? ToDateUtc,
        int? SezonaId,
        int? StoreId,
        string DataScope);

    public sealed record SnapshotAnalyticsComparisonTotals(
        decimal MarginContribution,
        double MarginPct,
        double CoveragePct,
        double NoCostPct,
        double LiveFallbackPct,
        double SnapshotCoveragePct);

    public sealed record SnapshotAnalyticsEntityDelta(
        int? EntityId,
        string EntityName,
        decimal Revenue,
        decimal LegacyMarginContribution,
        decimal SnapshotMarginContribution,
        decimal MarginContributionDelta,
        double LegacyMarginPct,
        double SnapshotMarginPct,
        double MarginPctDelta,
        double LegacyCoveragePct,
        double SnapshotCoveragePct,
        double CoveragePctDelta,
        int LegacyMarginContributionRank,
        int SnapshotMarginContributionRank,
        int MarginContributionRankDelta);

    public sealed record SnapshotAnalyticsComparisonResult(
        string ReportKey,
        SnapshotComparisonBatch Batch,
        SnapshotAnalyticsComparisonFilters Filters,
        bool FeatureFlagEnabled,
        int EntityCount,
        int ChangedEntityCount,
        SnapshotAnalyticsComparisonTotals Legacy,
        SnapshotAnalyticsComparisonTotals Snapshot,
        SnapshotAnalyticsComparisonTotals Delta,
        IReadOnlyList<SnapshotAnalyticsEntityDelta> LargestDeltas);

    public sealed record SnapshotHealthResult(
        bool FeatureFlagEnabled,
        bool AdminEnabled,
        bool HasActiveBatch,
        long? ActiveBatchId,
        string? ActiveBatchStatus,
        bool? ActiveBatchDryRun,
        string? Scope,
        DateTime? GeneratedAtUtc,
        DateTime? ActivatedAtUtc,
        int? RowCount,
        double? CoveragePct,
        double? NoCostPct,
        double? RemainingLiveFallbackPct,
        int? GenerationDurationMs,
        double? AgeHours,
        int StaleAfterHours,
        bool IsStale,
        string? Warning,
        long? LatestBatchId,
        string? LatestBatchStatus,
        bool? LatestBatchDryRun,
        DateTime? LatestBatchGeneratedAtUtc);
}
