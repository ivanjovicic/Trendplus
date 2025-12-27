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

        public async Task<IReadOnlyList<ErrorRecord>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await _db.Set<ErrorRecord>()
                            .AsNoTracking()
                            .OrderByDescending(x => x.Timestamp)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);
        }
    }
}
