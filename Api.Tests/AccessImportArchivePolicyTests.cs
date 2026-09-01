using Api.Config;
using Api.Services;
using Microsoft.Extensions.Configuration;
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
}
