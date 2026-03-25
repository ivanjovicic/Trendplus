using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Api.Services;
using Api.Services.Access;
using Xunit;

namespace Api.Tests
{
    public sealed class AccessImportEnqueueTests
    {
        private sealed class RecordingJobQueue : IAccessImportJobQueue
        {
            public int EnqueueCallCount { get; private set; }

            public Task EnqueueAsync(long batchId, CancellationToken ct = default)
            {
                EnqueueCallCount++;
                return Task.CompletedTask;
            }

            public Task<AccessImportQueuedJob?> ClaimNextAsync(CancellationToken ct = default)
                => Task.FromResult<AccessImportQueuedJob?>(null);
        }

        [Fact]
        public async Task StartImport_DoesNotEnqueueInlineAndCreatesPendingBatch()
        {
            // Arrange: in-memory Trendplus DB
            var options = new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var db = new TrendplusDbContext(options);

            // create a small temp file to satisfy File.Exists checks
            var tmp = Path.Combine(Path.GetTempPath(), $"test-import-{Guid.NewGuid():N}.accdb");
            File.WriteAllText(tmp, "");
            var queue = new RecordingJobQueue();

            var service = new AccessImportService(
                trendDb: db,
                analyticsDb: null!,
                logger: NullLogger<AccessImportService>.Instance,
                options: null,
                analyticsCache: null,
                serviceScopeFactory: null,
                jobQueue: queue);

            try
            {
                // Act
                var result = await service.StartImportAsync(tmp, includeAnalytics: false, overwriteExisting: false);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.BatchId > 0);
                Assert.Equal("pending", result.Status);
                Assert.Equal(0, queue.EnqueueCallCount);

                var batch = await db.DataImportBatches.SingleAsync();
                Assert.Equal("pending", batch.Status);
                Assert.False(batch.CancellationRequested);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
    }
}
