using Application.Common.Interfaces;
using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

public class ErrorStore : IErrorStore
{
    private readonly TrendplusDbContext _db;
    private readonly IHostEnvironment? _environment;

    public ErrorStore(TrendplusDbContext db, IHostEnvironment? environment = null)
    {
        _db = db;
        _environment = environment;
    }

    public async Task<IReadOnlyList<ErrorRecord>> GetAllAsync(
        string? level = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilters(level, fromDate, toDate, searchText)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(
        string? level = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilters(level, fromDate, toDate, searchText)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ErrorRecord>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? level = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var safePageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var safePageSize = pageSize <= 0 ? 50 : pageSize;

        return await ApplyFilters(level, fromDate, toDate, searchText)
            .OrderByDescending(e => e.Timestamp)
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<ErrorRecord?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _db.ErrorRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task SaveAsync(
        ErrorRecord error,
        CancellationToken cancellationToken = default)
    {
        if (!OperationalLogPersistencePolicy.ShouldPersist(_environment))
        {
            return;
        }

        _db.ErrorRecords.Add(error);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<ErrorRecord> ApplyFilters(
        string? level,
        DateTime? fromDate,
        DateTime? toDate,
        string? searchText)
    {
        var query = _db.ErrorRecords.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(level))
        {
            var normalizedLevel = level.Trim().ToUpperInvariant();
            query = query.Where(x => x.Level.ToUpper() == normalizedLevel);
        }

        if (fromDate.HasValue)
            query = query.Where(x => x.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.Timestamp <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = $"%{searchText.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(
                (x.Message ?? string.Empty) + " " +
                (x.ExceptionType ?? string.Empty) + " " +
                (x.StackTrace ?? string.Empty) + " " +
                (x.Path ?? string.Empty) + " " +
                (x.UserName ?? string.Empty) + " " +
                (x.ClientApp ?? string.Empty) + " " +
                (x.CorrelationId ?? string.Empty),
                term));
        }

        return query;
    }
}
