using Api.Models;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Manages batch-level import logs and enhanced batch querying.
/// Companion service to <see cref="IAccessImportService"/>.
/// </summary>
public interface IBatchLogService
{
    /// <summary>Log a single row-level event during import.</summary>
    void Log(long batchId, string tableName, int rowIndex, string severity, string message, string? sourceRowJson = null);

    /// <summary>Flush all buffered log entries to the database.</summary>
    Task FlushAsync(CancellationToken ct = default);

    /// <summary>Get batch detail including aggregated log counts.</summary>
    Task<BatchDetailDto?> GetBatchDetailAsync(long batchId, int logTake = 200, string? severityFilter = null, CancellationToken ct = default);

    /// <summary>Get logs for a specific batch with optional filtering.</summary>
    Task<List<AccessImportLogDto>> GetLogsAsync(long batchId, string? severity = null, string? tableName = null,
        int skip = 0, int take = 100, CancellationToken ct = default);

    /// <summary>Finalize a batch: set totals and duration from the run response.</summary>
    Task FinalizeBatchAsync(long batchId, AccessImportRunResponse result, CancellationToken ct = default);
}

public sealed class BatchLogService : IBatchLogService
{
    private readonly TrendplusDbContext _db;
    private readonly ILogger<BatchLogService> _logger;
    private readonly List<AccessImportLog> _buffer = new(256);
    private readonly object _lock = new();

    public BatchLogService(TrendplusDbContext db, ILogger<BatchLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void Log(long batchId, string tableName, int rowIndex, string severity, string message, string? sourceRowJson = null)
    {
        var entry = new AccessImportLog
        {
            BatchId = batchId,
            TableName = tableName,
            RowIndex = rowIndex,
            Severity = severity,
            Message = message.Length > 2000 ? message[..2000] : message,
            SourceRowJson = sourceRowJson,
            CreatedAtUtc = DateTime.UtcNow
        };

        lock (_lock)
        {
            _buffer.Add(entry);
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        List<AccessImportLog> snapshot;
        lock (_lock)
        {
            if (_buffer.Count == 0) return;
            snapshot = new List<AccessImportLog>(_buffer);
            _buffer.Clear();
        }

        try
        {
            _db.AccessImportLogs.AddRange(snapshot);
            await _db.SaveChangesAsync(ct);
            _logger.LogDebug("Flushed {Count} import log entries", snapshot.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} import log entries", snapshot.Count);
        }
    }

    public async Task FinalizeBatchAsync(long batchId, AccessImportRunResponse result, CancellationToken ct = default)
    {
        // Flush remaining log entries first
        await FlushAsync(ct);

        var batch = await _db.DataImportBatches.FindAsync([batchId], ct);
        if (batch is null) return;

        // Calculate totals from the run response
        batch.TotalImported =
            result.ArtikliInserted + result.DobavljaciInserted + result.SezoneInserted + result.TipoviInserted +
            result.ProdajaInserted + result.ProdajaStavkeInserted + result.DnevnikInserted +
            result.PovracajInserted + result.PovracajStavkeInserted +
            result.NivelacijeInserted + result.UnosRobeInserted + result.PovratnicaInserted +
            result.PrenosRobeInserted + result.ObjekatInserted +
            result.ProductsDimInserted + result.SalesFactsInserted + result.SalesLineFactsInserted + result.StoresInserted;

        batch.TotalUpdated =
            result.ArtikliUpdated + result.DobavljaciUpdated + result.SezoneUpdated + result.TipoviUpdated +
            result.ProdajaUpdated + result.ProdajaStavkeUpdated + result.DnevnikUpdated +
            result.PovracajUpdated + result.PovracajStavkeUpdated +
            result.ObjekatUpdated +
            result.ProductsDimUpdated + result.SalesFactsUpdated + result.StoresUpdated;

        batch.TotalErrors = await _db.AccessImportLogs
            .Where(l => l.BatchId == batchId && l.Severity == "error")
            .CountAsync(ct);

        if (batch.CompletedAtUtc.HasValue && batch.StartedAtUtc != default)
            batch.DurationSeconds = (int)(batch.CompletedAtUtc.Value - batch.StartedAtUtc).TotalSeconds;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<BatchDetailDto?> GetBatchDetailAsync(long batchId, int logTake = 200, string? severityFilter = null, CancellationToken ct = default)
    {
        var batch = await _db.DataImportBatches
            .AsNoTracking()
            .Where(b => b.Id == batchId)
            .Select(b => new AccessImportBatchDto
            {
                Id = b.Id,
                SourceSystem = b.SourceSystem,
                SourceFileName = b.SourceFileName,
                StartedAtUtc = b.StartedAtUtc,
                CompletedAtUtc = b.CompletedAtUtc,
                LastHeartbeatUtc = b.LastHeartbeatUtc,
                Status = b.Status,
                CurrentStep = b.CurrentStep,
                CurrentTable = b.CurrentTable,
                SummaryJson = b.SummaryJson,
                ErrorMessage = b.ErrorMessage,
                DurationSeconds = b.DurationSeconds,
                TotalImported = b.TotalImported,
                TotalUpdated = b.TotalUpdated,
                TotalErrors = b.TotalErrors,
                DataOrigin = b.DataOrigin
            })
            .FirstOrDefaultAsync(ct);

        if (batch is null) return null;

        var logQuery = _db.AccessImportLogs
            .AsNoTracking()
            .Where(l => l.BatchId == batchId);

        if (!string.IsNullOrEmpty(severityFilter))
            logQuery = logQuery.Where(l => l.Severity == severityFilter);

        var logs = await logQuery
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(logTake)
            .Select(l => new AccessImportLogDto
            {
                Id = l.Id,
                BatchId = l.BatchId,
                TableName = l.TableName,
                RowIndex = l.RowIndex,
                Severity = l.Severity,
                Message = l.Message,
                SourceRowJson = l.SourceRowJson,
                CreatedAtUtc = l.CreatedAtUtc
            })
            .ToListAsync(ct);

        var countBySeverity = await _db.AccessImportLogs
            .Where(l => l.BatchId == batchId)
            .GroupBy(l => l.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Severity, x => x.Count, ct);

        var countByTable = await _db.AccessImportLogs
            .Where(l => l.BatchId == batchId)
            .GroupBy(l => l.TableName)
            .Select(g => new { Table = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Table, x => x.Count, ct);

        return new BatchDetailDto
        {
            Batch = batch,
            Logs = logs,
            LogCountBySeverity = countBySeverity,
            LogCountByTable = countByTable
        };
    }

    public async Task<List<AccessImportLogDto>> GetLogsAsync(long batchId, string? severity = null, string? tableName = null,
        int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var query = _db.AccessImportLogs
            .AsNoTracking()
            .Where(l => l.BatchId == batchId);

        if (!string.IsNullOrEmpty(severity))
            query = query.Where(l => l.Severity == severity);

        if (!string.IsNullOrEmpty(tableName))
            query = query.Where(l => l.TableName == tableName);

        return await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 500))
            .Select(l => new AccessImportLogDto
            {
                Id = l.Id,
                BatchId = l.BatchId,
                TableName = l.TableName,
                RowIndex = l.RowIndex,
                Severity = l.Severity,
                Message = l.Message,
                SourceRowJson = l.SourceRowJson,
                CreatedAtUtc = l.CreatedAtUtc
            })
            .ToListAsync(ct);
    }
}
