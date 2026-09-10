using System.Reflection;
using Api.Config;
using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportArchivePolicyTests
{
    [Fact]
    public void ArchiveDeletedRows_is_disabled_by_default()
    {
        var options = new AccessImportOptions();

        Assert.False(options.ArchiveDeletedRows);
    }

    [Fact]
    public void ArchiveDeletedRows_can_be_explicitly_enabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AccessImport:ArchiveDeletedRows"] = "true"
            })
            .Build();
        var options = new AccessImportOptions();

        configuration.GetSection(AccessImportOptions.Section).Bind(options);

        Assert.True(options.ArchiveDeletedRows);
    }

    [Fact]
    public void Budget_blocks_when_existing_bytes_exceed_limit()
    {
        var decision = ArchiveStorageBudgetGuard.Evaluate(
            new ArchiveStorageBudgetSnapshot(17, 1),
            maxBytes: 16,
            maxRows: 10);

        Assert.False(decision.Allowed);
        Assert.Contains("byte budget exceeded", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Budget_blocks_when_existing_rows_exceed_limit()
    {
        var decision = ArchiveStorageBudgetGuard.Evaluate(
            new ArchiveStorageBudgetSnapshot(1, 11),
            maxBytes: 16,
            maxRows: 10);

        Assert.False(decision.Allowed);
        Assert.Contains("row budget exceeded", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Budget_rejects_non_positive_limits()
    {
        var decision = ArchiveStorageBudgetGuard.Evaluate(
            new ArchiveStorageBudgetSnapshot(0, 0),
            maxBytes: 0,
            maxRows: 10);

        Assert.False(decision.Allowed);
        Assert.Contains("invalid", decision.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("53100")] // disk_full
    [InlineData("42P01")] // undefined_table
    public async Task ArchiveWriteFailure_stops_batch_deletion(string sqlState)
    {
        await using var db = new TrendplusDbContext(
            new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var service = new AccessImportService(
            db,
            analyticsDb: null!,
            NullLogger<AccessImportService>.Instance,
            Options.Create(new AccessImportOptions { ArchiveDeletedRows = true }));
        var method = typeof(AccessImportService).GetMethod(
            "TryArchiveInsertCompatAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var postgresFailure = new PostgresException(
            "archive write failed",
            "ERROR",
            "ERROR",
            sqlState);
        Func<Task> archiveAction = () => Task.FromException(postgresFailure);

        var result = (Task)method!.Invoke(
            service,
            [archiveAction, "Artikli", "delete-batch-archive", 42L])!;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => result);

        Assert.Equal("Deleted-row archive write failed; batch deletion was stopped.", exception.Message);
        Assert.Same(postgresFailure, exception.InnerException);
    }
}
