using System.Diagnostics;
using Application.Analytics;
using Domain.Model.Analytics;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Services;

public sealed class AnalyticsCostSnapshotService
{
    private const string SupportedScope = "access_origin";
    private const int InsertBatchSize = 5000;

    private readonly TrendplusDbContext _db;
    private readonly ILogger<AnalyticsCostSnapshotService> _logger;

    public AnalyticsCostSnapshotService(
        TrendplusDbContext db,
        ILogger<AnalyticsCostSnapshotService> logger)
    {
        _db = db;
        _logger = logger;
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
            int totalEligible = resolvedCount + noCostCount;

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

            _logger.LogInformation(
                "Snapshot batch {BatchId} generation completed: {RowCount} rows, " +
                "{CoveragePct:F1}% coverage, {NoCostPct:F1}% no-cost, {DurationMs}ms (dryRun={DryRun})",
                batchId, resolvedCount, batch.CoveragePct, batch.NoCostPct,
                sw.ElapsedMilliseconds, dryRun);

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
        var activeBatch = await _db.AnalyticsCostSnapshotBatches
            .AsNoTracking()
            .Where(b => b.Status == "active" && b.Scope == SupportedScope)
            .FirstOrDefaultAsync(ct);

        double? ageDays = null;
        if (activeBatch?.GeneratedAtUtc is not null)
            ageDays = (DateTime.UtcNow - activeBatch.GeneratedAtUtc.Value).TotalDays;

        return new SnapshotHealthResult(
            HasActiveBatch: activeBatch is not null,
            ActiveBatchId: activeBatch?.Id,
            Scope: activeBatch?.Scope,
            GeneratedAtUtc: activeBatch?.GeneratedAtUtc,
            ActivatedAtUtc: activeBatch?.ActivatedAtUtc,
            RowCount: activeBatch?.RowCount,
            CoveragePct: activeBatch?.CoveragePct,
            AgeDays: ageDays is not null ? Math.Round(ageDays.Value, 1) : null,
            IsStale: ageDays is > 30);
    }

    // ── Result types ────────────────────────────────────────────────────

    public sealed record BatchDetailResult(
        AnalyticsCostSnapshotBatch Batch,
        Dictionary<string, int> CostSourceBreakdown);

    public sealed record SnapshotHealthResult(
        bool HasActiveBatch,
        long? ActiveBatchId,
        string? Scope,
        DateTime? GeneratedAtUtc,
        DateTime? ActivatedAtUtc,
        int? RowCount,
        double? CoveragePct,
        double? AgeDays,
        bool IsStale);
}
