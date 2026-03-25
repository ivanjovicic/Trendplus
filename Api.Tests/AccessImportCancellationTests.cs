using System;
using System.Threading.Tasks;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Api.Services;
using Xunit;

namespace Api.Tests
{
    public sealed class AccessImportCancellationTests
    {
        [Fact]
        public async Task MarkBatchInterruptedAsync_PersistsInterruptedStatus()
        {
            var options = new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var db = new TrendplusDbContext(options);

            var batch = new DataImportBatch
            {
                SourceSystem = "access",
                SourceFileName = "test.accdb",
                Status = "running",
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-30)
            };

            db.DataImportBatches.Add(batch);
            await db.SaveChangesAsync();

            var service = new AccessImportService(
                trendDb: db,
                analyticsDb: null!,
                logger: NullLogger<AccessImportService>.Instance,
                options: null,
                analyticsCache: null,
                serviceScopeFactory: null,
                jobQueue: null);

            await service.MarkBatchInterruptedAsync(batch.Id);

            var updated = await db.DataImportBatches.FirstAsync(x => x.Id == batch.Id);
            Assert.Equal("interrupted", updated.Status);
            Assert.Equal("stopped", updated.CurrentStep);
            Assert.Null(updated.CurrentTable);
            Assert.Equal(100, updated.ProgressPercent);
            Assert.NotNull(updated.CompletedAtUtc);
            Assert.NotNull(updated.LastHeartbeatUtc);
        }
    }
}
