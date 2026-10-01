using Api.Models;
using Xunit;

namespace Api.Tests;

public sealed class VendorSalesNivelacijaNullabilityContractTests
{
    [Fact]
    public void AggregateChangePercentFieldsRemainNullable()
    {
        Assert.Equal(
            typeof(decimal?),
            typeof(VendorSalesNivelacijaCategoryStatDto)
                .GetProperty(nameof(VendorSalesNivelacijaCategoryStatDto.ChangePercent))!
                .PropertyType);
        Assert.Equal(
            typeof(decimal?),
            typeof(VendorSalesNivelacijaPriceDirectionStatDto)
                .GetProperty(nameof(VendorSalesNivelacijaPriceDirectionStatDto.ChangePercent))!
                .PropertyType);
    }

    [Fact]
    public void PriceDirectionProjectionDoesNotTurnMissingBaselineIntoZero()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("ChangePercent = SemanticChangePercent(preRev, postRev),", source);
        Assert.DoesNotContain("ChangePercent = SemanticChangePercent(preRev, postRev) ?? 0m", source);
    }

    [Fact]
    public void SemanticRevenueColumnAndFailClosedSchemaGateRemainInContract()
    {
        var viewSql = ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");
        var endpoint = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("change_percent_revenue_semantic", viewSql);
        Assert.Contains("WHEN pre.pre_revenue = 0 THEN NULL", viewSql);
        Assert.Contains(
            "RelationHasColumnAsync(connection, \"vw_vendor_sales_nivelacija\", \"change_percent_revenue_semantic\", ct)",
            endpoint);
        Assert.Contains("vendor_sales_nivelacija_contract_missing", endpoint);
        Assert.Contains("pg_catalog.pg_attribute", endpoint);
        Assert.Contains("to_regclass(@rel)", endpoint);
        Assert.DoesNotContain("table_schema = 'public'", endpoint);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath)).ReplaceLineEndings("\n");
}
