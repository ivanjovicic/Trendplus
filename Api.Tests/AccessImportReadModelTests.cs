using System;
using System.Threading.Tasks;
using Api.Services;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests
{
    public sealed class AccessImportReadModelTests
    {
        [Fact]
        public async Task GetRecentBatchStatusesAsync_DoesNotReturnSummaryJson()
        {
            var options = new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var db = new TrendplusDbContext(options);
            db.DataImportBatches.Add(new DataImportBatch
            {
                SourceSystem = "access",
                SourceFileName = "sample.accdb",
                Status = "running",
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                QueuedAtUtc = DateTime.UtcNow.AddMinutes(-3),
                SummaryJson = "{\"heavy\":\"payload\"}"
            });
            await db.SaveChangesAsync();

            var service = new AccessImportService(
                trendDb: db,
                analyticsDb: null!,
                logger: NullLogger<AccessImportService>.Instance,
                options: null,
                analyticsCache: null,
                serviceScopeFactory: null,
                jobQueue: null);

            var rows = await service.GetRecentBatchStatusesAsync(20);
            Assert.Single(rows);
            Assert.Null(rows[0].SummaryJson);
        }
    }
}
