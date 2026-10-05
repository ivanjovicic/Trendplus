using Xunit;

namespace Api.Tests;

public sealed class OperationsAnalyticsCertificationWorkflowTests
{
    [Fact]
    public void AnalyticsTestsWorkflowDefinesNonSkippableRq453CertificationJob()
    {
        var source = ReadRepoFile(".github/workflows/analytics-tests.yml");

        Assert.Contains("rq453-operations-certification:", source);
        Assert.Contains("RQ453 Operations analytics certification", source);
        Assert.Contains("TRENDPLUS_RUN_INTEGRATION_TESTS: \"true\"", source);
        Assert.Contains("OperationsAnalyticsAllRoutesIntegrationTests", source);
        Assert.Contains("OperationsAnalyticsIntegrityFamilyTests", source);
        Assert.Contains("analyticsTrustStateProof.spec.tsx", source);
        Assert.Contains("rq453-certification-manifest.json", source);
        Assert.Contains("certification_trx_summary.py", source);
        Assert.DoesNotContain("rq447-certification:", source);
        Assert.Contains("RQ565", ReadRepoFile("scripts/ci/certification_trx_summary.py"));
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
