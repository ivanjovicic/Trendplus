using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Api.Services;
using Api.Services.Access;
using Api.Config;
using Domain.Model;
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

        [Fact]
        public async Task StartImport_WithFreshRunningBatch_ReturnsExistingBatchWithoutCreatingNew()
        {
            var options = new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var db = new TrendplusDbContext(options);
            var runningBatch = new DataImportBatch
            {
                SourceSystem = "access",
                SourceFileName = "already-running.accdb",
                Status = "running",
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-3),
                LastHeartbeatUtc = DateTime.UtcNow.AddSeconds(-10),
                QueuedAtUtc = DateTime.UtcNow.AddMinutes(-3)
            };
            db.DataImportBatches.Add(runningBatch);
            await db.SaveChangesAsync();

            var tmp = Path.Combine(Path.GetTempPath(), $"test-import-{Guid.NewGuid():N}.accdb");
            File.WriteAllText(tmp, "");
            var queue = new RecordingJobQueue();
            var serviceOptions = Options.Create(new AccessImportOptions
            {
                PreventConcurrentRuns = true,
                RunningBatchStaleMinutes = 240
            });

            var service = new AccessImportService(
                trendDb: db,
                analyticsDb: null!,
                logger: NullLogger<AccessImportService>.Instance,
                options: serviceOptions,
                analyticsCache: null,
                serviceScopeFactory: null,
                jobQueue: queue);

            try
            {
                var result = await service.StartImportAsync(tmp, includeAnalytics: true, overwriteExisting: true);

                Assert.Equal(runningBatch.Id, result.BatchId);
                Assert.Equal("running", result.Status);
                Assert.Single(db.DataImportBatches);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        [Fact]
        public async Task StartImport_WithStaleRunningBatch_CreatesNewPendingBatch()
        {
            var options = new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var db = new TrendplusDbContext(options);
            var staleBatch = new DataImportBatch
            {
                SourceSystem = "access",
                SourceFileName = "stale-running.accdb",
                Status = "running",
                StartedAtUtc = DateTime.UtcNow.AddHours(-10),
                LastHeartbeatUtc = DateTime.UtcNow.AddHours(-6),
                QueuedAtUtc = DateTime.UtcNow.AddHours(-10)
            };
            db.DataImportBatches.Add(staleBatch);
            await db.SaveChangesAsync();

            var tmp = Path.Combine(Path.GetTempPath(), $"test-import-{Guid.NewGuid():N}.accdb");
            File.WriteAllText(tmp, "");
            var queue = new RecordingJobQueue();
            var serviceOptions = Options.Create(new AccessImportOptions
            {
                PreventConcurrentRuns = true,
                RunningBatchStaleMinutes = 240
            });

            var service = new AccessImportService(
                trendDb: db,
                analyticsDb: null!,
                logger: NullLogger<AccessImportService>.Instance,
                options: serviceOptions,
                analyticsCache: null,
                serviceScopeFactory: null,
                jobQueue: queue);

            try
            {
                var result = await service.StartImportAsync(tmp, includeAnalytics: false, overwriteExisting: false);
                Assert.Equal("pending", result.Status);
                Assert.Equal(2, await db.DataImportBatches.CountAsync());
                Assert.NotEqual(staleBatch.Id, result.BatchId);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
    }
}
