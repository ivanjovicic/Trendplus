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
    public void AveragePriceChangeFieldsRemainNullableAndUseObservedValuesOnly()
    {
        Assert.Equal(
            typeof(decimal?),
            typeof(VendorSalesNivelacijaTotalsDto)
                .GetProperty(nameof(VendorSalesNivelacijaTotalsDto.AvgPriceChangePercent))!
                .PropertyType);
        Assert.Equal(
            typeof(decimal?),
            typeof(VendorSalesNivelacijaPriceDirectionStatDto)
                .GetProperty(nameof(VendorSalesNivelacijaPriceDirectionStatDto.AvgPriceChangePercent))!
                .PropertyType);

        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        Assert.Contains("ComputeAveragePriceChangePercent(", source);
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
        var contractInspector = ReadRepoFile("Api/Services/VendorSalesNivelacijaContractInspector.cs");
        var relationInspector = ReadRepoFile("Infrastructure/Database/PostgresRelationInspector.cs");
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");

        Assert.Contains("change_percent_revenue_semantic", viewSql);
        Assert.Contains("WHEN pre.pre_revenue = 0 THEN NULL", viewSql);
        Assert.Contains(
            "PostgresRelationInspector.InspectAsync(",
            endpoint);
        Assert.Contains("VendorSalesNivelacijaContractInspector.FindIssue(relationInspection)", endpoint);
        Assert.Contains("relationInspection.Columns.Contains(\"old_price\", StringComparer.Ordinal)", endpoint);
        Assert.Contains("vendor_sales_nivelacija_contract_missing", contractInspector);
        Assert.Contains("vendor_sales_nivelacija_privilege_missing", contractInspector);
        Assert.Contains("pg_catalog.pg_attribute", relationInspector);
        Assert.Contains("to_regclass(@relationName)", relationInspector);
        Assert.Contains("PostgresRelationInspector.InspectAsync(", initializer);
        Assert.DoesNotContain("public.vw_vendor_sales_nivelacija", initializer);
        Assert.Contains("ContractDiagnostic = meta.ContractDiagnostic", endpoint);
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
