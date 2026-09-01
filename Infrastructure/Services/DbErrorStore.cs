using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class DbErrorStore : IErrorStore
    {
        private const int MessageMaxLength = 2000;
        private const int ExceptionTypeMaxLength = 500;
        private const int StackTraceMaxLength = 4000;
        private const int PathMaxLength = 1000;
        private const int UserNameMaxLength = 200;
        private const int ClientAppMaxLength = 1000;

        private readonly IDbContextFactory<TrendplusDbContext> _dbFactory;
        private readonly ILogger<DbErrorStore> _logger;
        private readonly IHostEnvironment? _environment;

        public DbErrorStore(
            IDbContextFactory<TrendplusDbContext> dbFactory,
            ILogger<DbErrorStore> logger,
            IHostEnvironment? environment = null)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment;
        }

        public async Task<int> GetCountAsync(
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default)
        {
            using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await ApplyFilters(db, level, fromDate, toDate, searchText)
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

            using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await ApplyFilters(db, level, fromDate, toDate, searchText)
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
            using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await db.Set<ErrorRecord>()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                            .ConfigureAwait(false);
        }

        public async Task SaveAsync(ErrorRecord record, CancellationToken cancellationToken)
        {
            if (!OperationalLogPersistencePolicy.ShouldPersist(_environment))
            {
                return;
            }

            try
            {
                using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                record.Timestamp = record.Timestamp == default ? DateTime.UtcNow : record.Timestamp;
                NormalizeForStorage(record);
                db.Set<ErrorRecord>().Add(record);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
            using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await ApplyFilters(db, level, fromDate, toDate, searchText)
                            .OrderByDescending(x => x.Timestamp)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);
        }

        private IQueryable<ErrorRecord> ApplyFilters(
            TrendplusDbContext db,
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null)
        {
            var query = db.Set<ErrorRecord>().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(level))
            {
                var normalizedLevel = level.Trim().ToUpperInvariant();
                query = query.Where(x => x.Level.ToUpper() == normalizedLevel);
            }

            if (fromDate.HasValue)
            {
                var utcFrom = fromDate.Value.Kind switch
                {
                    DateTimeKind.Utc => fromDate.Value,
                    DateTimeKind.Unspecified => DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc),
                    _ => fromDate.Value.ToUniversalTime()
                };
                query = query.Where(x => x.Timestamp >= utcFrom);
            }

            if (toDate.HasValue)
            {
                var utcTo = toDate.Value.Kind switch
                {
                    DateTimeKind.Utc => toDate.Value,
                    DateTimeKind.Unspecified => DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc),
                    _ => toDate.Value.ToUniversalTime()
                };
                query = query.Where(x => x.Timestamp <= utcTo);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = $"%{searchText.Trim()}%";
                query = query.Where(x =>
                    EF.Functions.ILike((x.Message ?? string.Empty), term) ||
                    EF.Functions.ILike((x.ExceptionType ?? string.Empty), term) ||
                    EF.Functions.ILike((x.StackTrace ?? string.Empty), term) ||
                    EF.Functions.ILike((x.Path ?? string.Empty), term) ||
                    EF.Functions.ILike((x.UserName ?? string.Empty), term) ||
                    EF.Functions.ILike((x.ClientApp ?? string.Empty), term) ||
                    EF.Functions.ILike((x.CorrelationId ?? string.Empty), term));
            }

            return query;
        }

        private static void NormalizeForStorage(ErrorRecord record)
        {
            record.Message = Truncate(record.Message, MessageMaxLength) ?? string.Empty;
            record.ExceptionType = Truncate(record.ExceptionType, ExceptionTypeMaxLength) ?? string.Empty;
            record.StackTrace = Truncate(record.StackTrace, StackTraceMaxLength);
            record.Path = Truncate(record.Path, PathMaxLength);
            record.UserName = Truncate(record.UserName, UserNameMaxLength);
            record.ClientApp = Truncate(record.ClientApp, ClientAppMaxLength);
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }
    }
}
