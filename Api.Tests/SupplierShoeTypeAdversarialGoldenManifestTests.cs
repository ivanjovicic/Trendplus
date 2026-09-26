using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Deterministic")]
public sealed class SupplierShoeTypeAdversarialGoldenManifestTests
{
    private const string ManifestRelativePath =
        "docs/qa/SUPPLIER_SHOETYPE_ADVERSARIAL_GOLDEN_MANIFEST_2026-09-26.json";

    [Fact]
    public void Manifest_IsValidHashedAndCoversTheRequiredAdversarialMatrix()
    {
        var repositoryRoot = FindRepositoryRoot();
        var manifestPath = Path.Combine(repositoryRoot, ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var manifestBytes = File.ReadAllBytes(manifestPath);
        using var manifest = JsonDocument.Parse(manifestBytes);

        Assert.Equal("supplier-shoetype-adversarial-golden-2026-09-26", manifest.RootElement.GetProperty("manifestId").GetString());
        Assert.Equal("SST-ACCURACY-1.0", manifest.RootElement.GetProperty("contractVersion").GetString());
        Assert.Equal(
            "Api.Tests/Fixtures/operations-analytics-all-routes-seed.sql",
            manifest.RootElement.GetProperty("fixture").GetString());

        var hashPath = Path.ChangeExtension(manifestPath, ".sha256");
        var declaredHash = File.ReadAllText(hashPath).Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var actualHash = Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
        Assert.Equal(actualHash, declaredHash);

        var cases = manifest.RootElement.GetProperty("cases")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("id").GetString()!,
                StringComparer.Ordinal);

        var requiredIds = new[]
        {
            "boundary-fractional-and-signed",
            "adjacent-day-boundary",
            "store-and-origin-scope",
            "previous-only-shoe-type",
            "known-labels-versus-null-identity",
            "frozen-attribution-after-master-mutation",
            "duplicate-import-replay",
            "mixed-cost-coverage",
            "non-positive-margin-denominator",
            "top-n-includes-unknown",
            "cache-before-after-import"
        };

        Assert.Equal(requiredIds.Length, cases.Count);
        Assert.All(requiredIds, id => Assert.Contains(id, cases));
        Assert.All(cases.Values, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("proofOwner").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("evidenceClassification").GetString()));
        });

        var boundary = cases["boundary-fractional-and-signed"].GetProperty("expected").GetProperty("certifiedRetail");
        Assert.Equal(9, boundary.GetProperty("lineCount").GetInt32());
        Assert.Equal(6, boundary.GetProperty("units").GetInt32());
        Assert.Equal(590m, boundary.GetProperty("revenueRsd").GetDecimal());
        Assert.Equal(400m, boundary.GetProperty("excludedRevenueRsd").GetDecimal());

        var identity = cases["known-labels-versus-null-identity"].GetProperty("expected");
        Assert.False(identity.GetProperty("knownNepoznato").GetProperty("isUnknown").GetBoolean());
        Assert.False(identity.GetProperty("knownBlank").GetProperty("isUnknown").GetBoolean());
        Assert.True(identity.GetProperty("unknown").GetProperty("isUnknown").GetBoolean());

        var topN = cases["top-n-includes-unknown"].GetProperty("expected");
        Assert.True(topN.GetProperty("unknownIncluded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, topN.GetProperty("rankedSupplierIds")[0].ValueKind);

        var nonPositive = cases["non-positive-margin-denominator"].GetProperty("expected");
        Assert.Equal("unavailable", nonPositive.GetProperty("marginContributionShare").GetString());
    }

    [Fact]
    public void SharedFixture_ContainsAdversarialRowsOutsideTheOriginalJulyWindow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixturePath = Path.Combine(
            repositoryRoot,
            "Api.Tests",
            "Fixtures",
            "operations-analytics-all-routes-seed.sql");
        var fixture = File.ReadAllText(fixturePath, Encoding.UTF8);

        Assert.Contains("RQ407-101", fixture, StringComparison.Ordinal);
        Assert.Contains("RQ446-LOWER", fixture, StringComparison.Ordinal);
        Assert.Contains("2026-09-01T23:59:59.999999Z", fixture, StringComparison.Ordinal);
        Assert.Contains("'  dUg  '", fixture, StringComparison.Ordinal);
        Assert.Contains("' KoReKcIjA '", fixture, StringComparison.Ordinal);
        Assert.Contains("'ADV-MUTATION'", fixture, StringComparison.Ordinal);
        Assert.Contains("supplier_id_at_sale", fixture, StringComparison.Ordinal);
        Assert.Contains("shoe_type_id_at_sale", fixture, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
