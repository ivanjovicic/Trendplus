using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Configuration;
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
        private sealed class InMemoryFileStorage : IFileStorage
        {
            private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
            public int UploadCallCount { get; private set; }

            public Task UploadAsync(string key, Stream content, CancellationToken ct = default)
            {
                UploadCallCount++;
                using var ms = new MemoryStream();
                content.CopyTo(ms);
                _objects[key] = ms.ToArray();
                return Task.CompletedTask;
            }

            public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
            {
                if (!_objects.TryGetValue(key, out var payload))
                    throw new FileNotFoundException("Object not found.", key);

                return Task.FromResult<Stream>(new MemoryStream(payload, writable: false));
            }

            public Task DeleteAsync(string key, CancellationToken ct = default)
            {
                _objects.Remove(key);
                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
                => Task.FromResult(_objects.ContainsKey(key));

            public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
                => Task.FromResult($"memory://{key}");
        }

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

            public Task<AccessImportEnqueueDiagnostics> GetEnqueueDiagnosticsAsync(long batchId, CancellationToken ct = default)
                => Task.FromResult(new AccessImportEnqueueDiagnostics(
                    batchId,
                    Exists: true,
                    CurrentStatus: "pending",
                    HasSourceFilePath: true,
                    HasSourceStorageKey: false,
                    CancellationRequested: false,
                    CompletedAtUtc: null,
                    Enqueueable: true,
                    Reason: "enqueueable"));

            public Task<AccessImportPendingRecoveryResult> RecoverStalePendingAsync(TimeSpan staleAfter, CancellationToken ct = default)
                => Task.FromResult(new AccessImportPendingRecoveryResult(0, 0, 0));
        }

        private sealed class HangingFileStorage : IFileStorage
        {
            public int UploadCallCount { get; private set; }

            public async Task UploadAsync(string key, Stream content, CancellationToken ct = default)
            {
                UploadCallCount++;
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task DeleteAsync(string key, CancellationToken ct = default)
                => Task.CompletedTask;

            public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
                => Task.FromResult(false);

            public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
                => throw new NotSupportedException();
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

        [Fact]
        public async Task StartImport_WithFileStorage_PersistsStorageBackedBatchMetadata()
        {
            var options = new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var db = new TrendplusDbContext(options);
            var tmp = Path.Combine(Path.GetTempPath(), $"test-import-{Guid.NewGuid():N}.accdb");
            File.WriteAllText(tmp, "dummy");
            var queue = new RecordingJobQueue();
            var storage = new InMemoryFileStorage();
            var storageOptions = Options.Create(new StorageOptions
            {
                Provider = "s3"
            });

            var service = new AccessImportService(
                trendDb: db,
                analyticsDb: null!,
                logger: NullLogger<AccessImportService>.Instance,
                options: null,
                analyticsCache: null,
                serviceScopeFactory: null,
                jobQueue: queue,
                cursorRepository: null,
                fileStorage: storage,
                storageOptions: storageOptions);

            try
            {
                var result = await service.StartImportAsync(tmp, includeAnalytics: false, overwriteExisting: false);

                Assert.NotNull(result);
                var batch = await db.DataImportBatches.SingleAsync();
                Assert.Equal("pending", batch.Status);
                Assert.True(string.IsNullOrWhiteSpace(batch.SourceFilePath));
                Assert.False(string.IsNullOrWhiteSpace(batch.SourceStorageKey));
                Assert.Equal("s3", batch.SourceStorageProvider);
                Assert.True(await storage.ExistsAsync(batch.SourceStorageKey!));
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        [Fact]
        public async Task StartImport_WithLocalFileStorage_KeepsLegacySourceFilePathFlow()
        {
            var options = new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var db = new TrendplusDbContext(options);
            var tmp = Path.Combine(Path.GetTempPath(), $"test-import-{Guid.NewGuid():N}.accdb");
            File.WriteAllText(tmp, "dummy");
            var queue = new RecordingJobQueue();
            var storage = new InMemoryFileStorage();
            var storageOptions = Options.Create(new StorageOptions
            {
                Provider = "local"
            });

            var service = new AccessImportService(
                trendDb: db,
                analyticsDb: null!,
                logger: NullLogger<AccessImportService>.Instance,
                options: null,
                analyticsCache: null,
                serviceScopeFactory: null,
                jobQueue: queue,
                cursorRepository: null,
                fileStorage: storage,
                storageOptions: storageOptions);

            try
            {
                var result = await service.StartImportAsync(tmp, includeAnalytics: false, overwriteExisting: false);

                Assert.NotNull(result);
                var batch = await db.DataImportBatches.SingleAsync();
                Assert.Equal("pending", batch.Status);
                Assert.False(string.IsNullOrWhiteSpace(batch.SourceFilePath));
                Assert.True(string.IsNullOrWhiteSpace(batch.SourceStorageKey));
                Assert.True(string.IsNullOrWhiteSpace(batch.SourceStorageProvider));
                Assert.Equal(0, storage.UploadCallCount);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        [Fact]
        public async Task StartImport_WhenStorageUploadTimesOut_DoesNotCreateCorruptBatch()
        {
            var options = new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var db = new TrendplusDbContext(options);
            var tmp = Path.Combine(Path.GetTempPath(), $"test-import-{Guid.NewGuid():N}.accdb");
            File.WriteAllText(tmp, "dummy");
            var queue = new RecordingJobQueue();
            var storage = new HangingFileStorage();
            var storageOptions = Options.Create(new StorageOptions
            {
                Provider = "s3",
                UploadTimeoutSeconds = 1
            });

            var service = new AccessImportService(
                trendDb: db,
                analyticsDb: null!,
                logger: NullLogger<AccessImportService>.Instance,
                options: null,
                analyticsCache: null,
                serviceScopeFactory: null,
                jobQueue: queue,
                cursorRepository: null,
                fileStorage: storage,
                storageOptions: storageOptions);

            try
            {
                var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
                    service.StartImportAsync(tmp, includeAnalytics: false, overwriteExisting: false));

                Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(1, storage.UploadCallCount);
                Assert.Equal(0, await db.DataImportBatches.CountAsync());
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
    }
}
