using Xunit;

namespace Api.Tests;

public sealed class AnalyticsAggregationWorkerAtomicityTests
{
    [Fact]
    public void AllDeleteInsertAggregateReplacementsUseTheSharedTransactionOwner()
    {
        var worker = ReadRepoFile("Workers/AnalyticsAggregationWorker.cs");
        const string replacementCall = "await ExecuteAggregateReplacementTransactionAsync(connection, date, deleteSql, insertSql, ct);";

        Assert.Equal(4, worker.Split(replacementCall, StringSplitOptions.None).Length - 1);
        Assert.Contains("BeginTransactionAsync(ct)", worker, StringComparison.Ordinal);
        Assert.Contains("new NpgsqlCommand(deleteSql, connection, transaction)", worker, StringComparison.Ordinal);
        Assert.Contains("new NpgsqlCommand(insertSql, connection, transaction)", worker, StringComparison.Ordinal);
        Assert.Contains("await transaction.CommitAsync(ct);", worker, StringComparison.Ordinal);
        Assert.Contains("await transaction.RollbackAsync(CancellationToken.None);", worker, StringComparison.Ordinal);
        Assert.Contains("AggregateReplacementMaxAttempts", worker, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
