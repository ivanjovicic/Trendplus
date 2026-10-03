using System.Text.RegularExpressions;
using Api.Models;
using Api.Services;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Access "Random AutoNumber" produces negative supplier, store, shoe type and season IDs
/// (e.g. supplier BIS -2122024036, shoe type Ž.Cipela -2004188974, store -598733481).
/// </summary>
public sealed class NegativeEntityIdGuardTests
{
    private static readonly Regex SignBasedEntityIdCheck = new(
        @"((Supplier|Dobavljac|Store|Objekat|Season|Sezona|TipObuce|ShoeType|FootwearType|Vendor)I[dD]\w*|ID(Dobavljac|Objekat|Sezona|TipObuce))(\.Value)?\)?\s*(is\s*(not\s*)?)?(>|>=|<=|<)\s*0\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NullableEntitySentinelCheck = new(
        "COALESCE\\s*\\(\\s*(?:vendor_id|(?:[A-Za-z_]\\w*\\.)?\\\"IDObjekat\\\")\\s*,\\s*-1\\s*\\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] AllowList =
    [
    ];

    [Fact]
    public void BackendSourceDoesNotTreatNonPositiveEntityIdsAsUnknown()
    {
        var repoRoot = FindRepoRoot();
        var violations = new List<string>();

        foreach (var folder in new[] { "Api", "Application", "Infrastructure", "Domain" })
        {
            var root = Path.Combine(repoRoot, folder);
            if (!Directory.Exists(root))
                continue;

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/") || relative.Contains("/Migrations/") || relative.Contains("/.tmp_"))
                    continue;

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!SignBasedEntityIdCheck.IsMatch(lines[i]))
                        continue;

                    var location = $"{relative}:{i + 1}";
                    if (!AllowList.Contains(location))
                        violations.Add($"{location}: {lines[i].Trim()}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Entity IDs may be negative (Access Random AutoNumber). Use HasValue / master-row lookup instead of a sign check:\n"
            + string.Join('\n', violations));
    }

    [Fact]
    public void AnalyticsSqlDoesNotMapNullableEntityIdsToNegativeOne()
    {
        var repoRoot = FindRepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(repoRoot, "Api"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(repoRoot, "Database", "Analytics"), "*.sql", SearchOption.AllDirectories));
        var violations = files
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(item => NullableEntitySentinelCheck.IsMatch(item.line)))
            .Select(item => $"{Path.GetRelativePath(repoRoot, item.file).Replace('\\', '/') }:{item.index + 1}: {item.line.Trim()}")
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Nullable entity IDs must remain distinct from real negative IDs; use native NULL-safe grouping/joins:\n"
            + string.Join('\n', violations));
    }

    [Theory]
    [InlineData("if (supplierId is > 0)")]
    [InlineData(".Where(x => x.StoreId is > 0)")]
    [InlineData("if (candidate.FootwearTypeId is not > 0")]
    [InlineData("where article.IDDobavljac.Value > 0")]
    public void GuardPatternFlagsSignChecks(string sample)
    {
        Assert.Matches(SignBasedEntityIdCheck, sample);
    }

    [Fact]
    public void EntityIdentityKeepsNegativeIdsAssignedAndLabelsThem()
    {
        Assert.True(EntityIdentity.IsAssigned(-2122024036));
        Assert.True(EntityIdentity.IsAssigned(0));
        Assert.False(EntityIdentity.IsAssigned(null));
        Assert.Equal("Nepoznat objekat (ID -598733481)", EntityIdentity.FallbackLabel(EntityKind.Store, -598733481));
        Assert.Equal("Nepoznat dobavljač", EntityIdentity.FallbackLabel(EntityKind.Supplier, null));
    }

    [Fact]
    public void PreNivelacijaFacetsKeepNegativeAccessIds()
    {
        var candidate = new PreNivelacijaSkuCandidateDto
        {
            ArtikalId = 1,
            Sku = "SKU-1",
            StoreId = -598733481,
            StoreName = "Objekat Negativni",
            SupplierId = -2122024036,
            SupplierName = "BIS",
            SeasonId = -7,
            Season = "Arhivska sezona",
            FootwearTypeId = -2004188974,
            FootwearType = "Ž.Cipela",
        };

        var facets = PreNivelacijaPriorityEndpoints.BuildFilterFacets([candidate], null, null, null);
        var filtered = PreNivelacijaPriorityEndpoints.ApplyDimensionFilters([candidate], -2122024036, -7, -2004188974, storeId: -598733481);

        Assert.Contains(facets.Stores, option => option.Id == -598733481 && option.Label == "Objekat Negativni");
        Assert.Contains(facets.Suppliers, option => option.Id == -2122024036 && option.Label == "BIS");
        Assert.Contains(facets.Seasons, option => option.Id == -7);
        Assert.Contains(facets.FootwearTypes, option => option.Id == -2004188974 && option.Label == "Ž.Cipela");
        Assert.Single(filtered);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
