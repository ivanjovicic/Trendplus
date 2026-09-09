using Xunit;

namespace Api.Tests;

public sealed class AccessImportRetryAtomicityTests
{
    [Fact]
    public void AnalyticsProjectionUsesOneRollbackBoundaryForTheWholeBatch()
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
        Assert.Equal(1, projection.Split("BeginTransactionAsync(ct)", StringSplitOptions.None).Length - 1);
        Assert.Contains("await transaction.CommitAsync(ct);", projection, StringComparison.Ordinal);
        Assert.Contains("await transaction.RollbackAsync(CancellationToken.None);", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("stepTx", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("RunStepAsync", projection, StringComparison.Ordinal);
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
