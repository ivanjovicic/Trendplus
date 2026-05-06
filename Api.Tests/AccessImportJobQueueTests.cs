using Api.Services.Access;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportJobQueueTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AccessImportJobQueueTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ClaimNextAsync_ClaimsPendingBatch_WithSourceFilePath()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
            return;

        var batch = await InsertBatchAsync(db, sourceFilePath: "/tmp/import.accdb");
        var queue = CreateQueue(db);

        var job = await queue.ClaimNextAsync();

        Assert.NotNull(job);
        Assert.Equal(batch.Id, job.BatchId);
        Assert.Equal("/tmp/import.accdb", job.SourceFilePath);
        var updated = await db.DataImportBatches.AsNoTracking().SingleAsync(x => x.Id == batch.Id);
        Assert.Equal("running", updated.Status);
        Assert.Equal("starting", updated.CurrentStep);
        Assert.NotNull(updated.LastHeartbeatUtc);
    }

    [Fact]
    public async Task ClaimNextAsync_ClaimsPendingBatch_WithSourceStorageKey()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
            return;

        var batch = await InsertBatchAsync(db, sourceStorageKey: "access/imports/source.accdb", sourceStorageProvider: "s3");
        var queue = CreateQueue(db);

        var job = await queue.ClaimNextAsync();

        Assert.NotNull(job);
        Assert.Equal(batch.Id, job.BatchId);
        Assert.Equal("access/imports/source.accdb", job.SourceStorageKey);
        Assert.Equal("s3", job.SourceStorageProvider);
        var updated = await db.DataImportBatches.AsNoTracking().SingleAsync(x => x.Id == batch.Id);
        Assert.Equal("running", updated.Status);
    }

    [Fact]
    public async Task ClaimNextAsync_DoesNotClaimCancelledBatch()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
            return;

        var batch = await InsertBatchAsync(db, sourceFilePath: "/tmp/import.accdb", cancellationRequested: true);
        var queue = CreateQueue(db);

        var job = await queue.ClaimNextAsync();

        Assert.Null(job);
        var updated = await db.DataImportBatches.AsNoTracking().SingleAsync(x => x.Id == batch.Id);
        Assert.Equal("pending", updated.Status);
    }

    [Fact]
    public async Task ClaimNextAsync_DoesNotClaimCompletedBatch()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
            return;

        var completedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var batch = await InsertBatchAsync(db, sourceFilePath: "/tmp/import.accdb", completedAtUtc: completedAtUtc);
        var queue = CreateQueue(db);

        var job = await queue.ClaimNextAsync();

        Assert.Null(job);
        var updated = await db.DataImportBatches.AsNoTracking().SingleAsync(x => x.Id == batch.Id);
        Assert.Equal("pending", updated.Status);
        Assert.NotNull(updated.CompletedAtUtc);
        Assert.True(Math.Abs((updated.CompletedAtUtc.Value - completedAtUtc).TotalSeconds) < 1);
    }

    [Fact]
    public async Task RecoverStalePendingAsync_DetectsAndRefreshesClaimablePendingBatch()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
            return;

        var queuedAtUtc = DateTime.UtcNow.AddMinutes(-30);
        var batch = await InsertBatchAsync(db, sourceFilePath: "/tmp/import.accdb", queuedAtUtc: queuedAtUtc, lastHeartbeatUtc: queuedAtUtc);
        var queue = CreateQueue(db);

        var result = await queue.RecoverStalePendingAsync(TimeSpan.FromMinutes(10));

        Assert.Equal(1, result.StalePendingCount);
        Assert.Equal(1, result.RecoveredCount);
        Assert.Equal(0, result.MissingSourceCount);
        var updated = await db.DataImportBatches.AsNoTracking().SingleAsync(x => x.Id == batch.Id);
        Assert.Equal("pending", updated.Status);
        Assert.Equal("queued-stale-recovered", updated.CurrentStep);
        Assert.True(updated.LastHeartbeatUtc > queuedAtUtc);
    }

    [Fact]
    public async Task GetEnqueueDiagnosticsAsync_ReturnsUsefulReason_WhenSourceIsMissing()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
            return;

        var batch = await InsertBatchAsync(db);
        var queue = CreateQueue(db);

        var diagnostics = await queue.GetEnqueueDiagnosticsAsync(batch.Id);

        Assert.True(diagnostics.Exists);
        Assert.Equal("pending", diagnostics.CurrentStatus);
        Assert.False(diagnostics.HasSourceFilePath);
        Assert.False(diagnostics.HasSourceStorageKey);
        Assert.False(diagnostics.Enqueueable);
        Assert.Equal("missing_source", diagnostics.Reason);
    }

    private async Task<TrendplusDbContext?> CreateDatabaseAsync()
    {
        if (!_fixture.IsAvailable)
            return null;

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_access_queue_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var db = new TrendplusDbContext(options);
        await BootstrapQueueSchemaAsync(db);
        return db;
    }

    private static AccessImportJobQueue CreateQueue(TrendplusDbContext db)
        => new(db, NullLogger<AccessImportJobQueue>.Instance);

    private static async Task<DataImportBatch> InsertBatchAsync(
        TrendplusDbContext db,
        string? sourceFilePath = null,
        string? sourceStorageKey = null,
        string? sourceStorageProvider = null,
        bool cancellationRequested = false,
        DateTime? completedAtUtc = null,
        DateTime? queuedAtUtc = null,
        DateTime? lastHeartbeatUtc = null)
    {
        var now = DateTime.UtcNow;
        var batch = new DataImportBatch
        {
            SourceSystem = "access",
            SourceFileName = "import.accdb",
            SourceFilePath = sourceFilePath,
            SourceStorageKey = sourceStorageKey,
            SourceStorageProvider = sourceStorageProvider,
            QueuedAtUtc = queuedAtUtc ?? now,
            StartedAtUtc = queuedAtUtc ?? now,
            CompletedAtUtc = completedAtUtc,
            LastHeartbeatUtc = lastHeartbeatUtc ?? queuedAtUtc ?? now,
            Status = "pending",
            CurrentStep = "queued",
            CurrentTable = "all",
            IncludeAnalytics = true,
            OverwriteExisting = true,
            IncludeTemporaryTables = false,
            CancellationRequested = cancellationRequested
        };

        db.DataImportBatches.Add(batch);
        await db.SaveChangesAsync();
        return batch;
    }

    private static Task BootstrapQueueSchemaAsync(TrendplusDbContext db)
        => db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "DataImportBatches" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SourceSystem" character varying(64) NOT NULL DEFAULT 'access',
                "SourceFileName" character varying(300) NOT NULL DEFAULT '',
                "SourceFilePath" character varying(800),
                "SourceStorageKey" character varying(1024),
                "SourceStorageProvider" character varying(32),
                "QueuedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "StartedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "CompletedAtUtc" timestamp with time zone,
                "LastHeartbeatUtc" timestamp with time zone,
                "Status" character varying(32) NOT NULL DEFAULT 'pending',
                "CurrentStep" character varying(64),
                "CurrentTable" character varying(300),
                "SummaryJson" text,
                "ErrorMessage" character varying(4000),
                "ErrorDetailsJson" text,
                "RequestedBy" character varying(200),
                "ImportMode" character varying(16) NOT NULL DEFAULT 'auto',
                "ImportStrategy" character varying(32) NOT NULL DEFAULT 'full',
                "IncludeAnalytics" boolean NOT NULL DEFAULT TRUE,
                "OverwriteExisting" boolean NOT NULL DEFAULT TRUE,
                "IncludeTemporaryTables" boolean NOT NULL DEFAULT FALSE,
                "SkipInvalidForeignKeys" boolean NOT NULL DEFAULT TRUE,
                "CancellationRequested" boolean NOT NULL DEFAULT FALSE,
                "CancellationRequestedAtUtc" timestamp with time zone,
                "RetryCount" integer NOT NULL DEFAULT 0,
                "ProgressPercent" integer NOT NULL DEFAULT 0,
                "RowsRead" integer NOT NULL DEFAULT 0,
                "RowsAccepted" integer NOT NULL DEFAULT 0,
                "RowsWritten" integer NOT NULL DEFAULT 0,
                "IsIncremental" boolean NOT NULL DEFAULT FALSE,
                "CursorSnapshot" jsonb,
                "CursorBeforeJson" jsonb,
                "CursorAfterJson" jsonb,
                "ProcessedRowCount" integer NOT NULL DEFAULT 0,
                "SkippedRowCount" integer NOT NULL DEFAULT 0,
                "RowsInserted" integer NOT NULL DEFAULT 0,
                "RowsUpdated" integer NOT NULL DEFAULT 0,
                "RowsUnchanged" integer NOT NULL DEFAULT 0,
                "RowsStaged" integer NOT NULL DEFAULT 0,
                "RowsSkippedStale" integer NOT NULL DEFAULT 0,
                "RowsRejected" integer NOT NULL DEFAULT 0,
                "ShadowMismatchCount" integer NOT NULL DEFAULT 0,
                "SourceFileHash" character varying(128),
                "DurationSeconds" integer,
                "TotalImported" integer NOT NULL DEFAULT 0,
                "TotalUpdated" integer NOT NULL DEFAULT 0,
                "TotalErrors" integer NOT NULL DEFAULT 0,
                "DataOrigin" character varying(32) NOT NULL DEFAULT 'access'
            );
            """);
}
