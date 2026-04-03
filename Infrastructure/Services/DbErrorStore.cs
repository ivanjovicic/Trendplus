using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class DbErrorStore : IErrorStore
    {
        private readonly TrendplusDbContext _db;
        private readonly ILogger<DbErrorStore> _logger;

        public DbErrorStore(TrendplusDbContext db, ILogger<DbErrorStore> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SaveAsync(ErrorRecord record, CancellationToken cancellationToken)
        {
            try
            {
                record.Timestamp = record.Timestamp == default ? DateTime.UtcNow : record.Timestamp;
                _db.Set<ErrorRecord>().Add(record);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Swallow but log: we must not throw from the logging path
                _logger.LogError(ex, "Failed to persist ErrorRecord to DB");
            }
        }

        public async Task<IReadOnlyList<ErrorRecord>> GetAllAsync(
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default)
        {
            return await ApplyFilters(level, fromDate, toDate, searchText)
                            .OrderByDescending(x => x.Timestamp)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);
        }

        public async Task<int> GetCountAsync(
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default)
        {
            return await ApplyFilters(level, fromDate, toDate, searchText)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);
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
                            .OrderByDescending(x => x.Timestamp)
                            .Skip((safePageNumber - 1) * safePageSize)
                            .Take(safePageSize)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);
        }

        public async Task<ErrorRecord?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _db.Set<ErrorRecord>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }

        private IQueryable<ErrorRecord> ApplyFilters(
            string? level,
            DateTime? fromDate,
            DateTime? toDate,
            string? searchText)
        {
            var query = _db.Set<ErrorRecord>().AsNoTracking();

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
}
