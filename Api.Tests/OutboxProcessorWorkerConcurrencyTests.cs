using System.Reflection;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Workers;
using Xunit;

namespace Api.Tests;

public sealed class OutboxProcessorWorkerConcurrencyTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public OutboxProcessorWorkerConcurrencyTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void WorkerAndAnalyticsModelDeclareRowLockingAndLineIdempotency()
    {
        var worker = ReadRepoFile("Workers/OutboxProcessorWorker.cs");
        var analyticsContext = ReadRepoFile("Infrastructure/DbContexts/AnalyticsDbContext.cs");
        var migration = ReadRepoFile("Infrastructure/Migrations/AnalyticsDb/20260909190000_AddSalesLineFactIdempotency.cs");

        Assert.Contains("FOR UPDATE SKIP LOCKED", worker, StringComparison.Ordinal);
        Assert.Contains("BeginTransactionAsync(ct)", worker, StringComparison.Ordinal);
        Assert.Contains("await transaction.CommitAsync(ct);", worker, StringComparison.Ordinal);
        Assert.Contains("await transaction.RollbackAsync(CancellationToken.None);", worker, StringComparison.Ordinal);
        Assert.Contains("new { e.SaleId, e.ProductId }).IsUnique()", analyticsContext, StringComparison.Ordinal);
        Assert.Contains("IX_SalesLineFacts_ProductId_SaleId", migration, StringComparison.Ordinal);
        Assert.Contains("IX_SalesLineFacts_SaleId_ProductId", migration, StringComparison.Ordinal);
        Assert.Contains("unique: true", migration, StringComparison.Ordinal);
        Assert.Contains("duplicate (SaleId, ProductId)", migration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentTransactionsClaimPendingMessageOnlyOnce()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_outbox_lock_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var setupConnection = new NpgsqlConnection(connectionString);
        await setupConnection.OpenAsync();
        await using (var setupCommand = new NpgsqlCommand("""
            CREATE TABLE "OutboxMessages" (
                "Id" bigint PRIMARY KEY,
                "EventType" varchar(200) NOT NULL,
                "Payload" text NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ProcessedAt" timestamp with time zone NULL,
                "IsProcessed" boolean NOT NULL,
                "RetryCount" integer NOT NULL,
                "ErrorMessage" varchar(2000) NULL,
                "CorrelationId" varchar(100) NULL
            );
            INSERT INTO "OutboxMessages"
                ("Id", "EventType", "Payload", "CreatedAt", "IsProcessed", "RetryCount")
            VALUES (1, 'ProdajaKreirana', '{}', now(), false, 0);
            """, setupConnection))
        {
            await setupCommand.ExecuteNonQueryAsync();
        }

        await using var db1 = CreateDbContext(connectionString);
        await using var db2 = CreateDbContext(connectionString);
        await using var transaction1 = await db1.Database.BeginTransactionAsync();
        await using var transaction2 = await db2.Database.BeginTransactionAsync();

        var firstClaim = await ClaimPendingMessagesAsync(db1);
        var secondClaim = await ClaimPendingMessagesAsync(db2);

        Assert.Single(firstClaim);
        Assert.Empty(secondClaim);
    }

    private static Task<List<OutboxMessage>> ClaimPendingMessagesAsync(TrendplusDbContext db)
    {
        var method = typeof(OutboxProcessorWorker).GetMethod(
            "ClaimPendingMessagesAsync",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var task = method!.Invoke(null, new object[] { db, CancellationToken.None });
        Assert.IsType<Task<List<OutboxMessage>>>(task);
        return (Task<List<OutboxMessage>>)task!;
    }

    private static TrendplusDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TrendplusDbContext(options);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
