using Xunit;

namespace Api.Tests;

public sealed class AccessImportRetryAtomicityTests
{
    [Fact]
    public void AnalyticsProjectionEnlistsInOuterOperationalTransaction()
    {
        var service = ReadRepoFile("Api/Services/AccessImportService.cs");
        var projectionStart = service.IndexOf(
            "private async Task ApplyAnalyticsFastPayloadAsync(",
            StringComparison.Ordinal);
        var projectionEnd = service.IndexOf(
            "    private async Task UpsertProductsDimBulkAsync(",
            projectionStart,
            StringComparison.Ordinal);

        Assert.True(projectionStart >= 0);
        Assert.True(projectionEnd > projectionStart);

        var projection = service[projectionStart..projectionEnd];
        Assert.Contains("_trendDb.Database.CurrentTransaction", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginTransactionAsync", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("CommitAsync", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("RollbackAsync", projection, StringComparison.Ordinal);

        var batchStart = service.IndexOf("private async Task<AccessImportRunResponse> ExecuteImportBatchAsync(", StringComparison.Ordinal);
        var batchEnd = service.IndexOf("private sealed record", batchStart, StringComparison.Ordinal);
        Assert.True(batchStart >= 0);
        Assert.True(batchEnd > batchStart);
        var batch = service[batchStart..batchEnd];
        Assert.Contains("RetriableDbContextTransaction.ExecuteAsync(_trendDb", batch, StringComparison.Ordinal);
        Assert.Contains("EnsureAnalyticsSharesTrendplusDatabase();", batch, StringComparison.Ordinal);
        Assert.Contains("EnlistAnalyticsContextInTrendTransactionAsync(transactionCt)", batch, StringComparison.Ordinal);
        Assert.True(batch.IndexOf("EnlistAnalyticsContextInTrendTransactionAsync", StringComparison.Ordinal)
            < batch.IndexOf("ImportTrendplusAsync(session", StringComparison.Ordinal));
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
