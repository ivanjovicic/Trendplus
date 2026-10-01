using Infrastructure.Database;
using Trendplus2.Dtos;

namespace Api.Services;

public sealed record VendorSalesNivelacijaContractIssue(
    string Relation,
    string MissingPart,
    string? Schema,
    string? MissingColumn,
    IReadOnlyList<string>? FoundInSchemas,
    string ErrorCode,
    string ErrorMessage)
{
    public AnalyticsContractDiagnosticDto ToDto() => new()
    {
        Relation = Relation,
        MissingPart = MissingPart,
        Schema = Schema,
        MissingColumn = MissingColumn,
        FoundInSchemas = FoundInSchemas
    };
}

public static class VendorSalesNivelacijaContractInspector
{
    public const string RelationName = "vw_vendor_sales_nivelacija";
    public const string RequiredRevenueColumn = "change_percent_revenue_semantic";

    public static VendorSalesNivelacijaContractIssue? FindIssue(PostgresRelationInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);

        if (!inspection.IsResolved)
        {
            if (inspection.SchemasWithRelation.Length > 0)
            {
                return new VendorSalesNivelacijaContractIssue(
                    RelationName,
                    "schema",
                    null,
                    null,
                    inspection.SchemasWithRelation,
                    "vendor_sales_nivelacija_contract_missing",
                    $"Pre/post nivelacija nije dostupna: relacija {RelationName} postoji van aktivnog search_path-a.");
            }

            return new VendorSalesNivelacijaContractIssue(
                RelationName,
                "relation",
                null,
                null,
                null,
                "vendor_sales_nivelacija_contract_missing",
                $"Pre/post nivelacija nije dostupna: relacija {RelationName} ne postoji.");
        }

        if (!inspection.HasSelectPrivilege)
        {
            return new VendorSalesNivelacijaContractIssue(
                RelationName,
                "privilege",
                inspection.ResolvedSchema,
                null,
                null,
                "vendor_sales_nivelacija_privilege_missing",
                $"Pre/post nivelacija nije dostupna: API uloga nema SELECT privilegiju nad relacijom {inspection.ResolvedSchema}.{RelationName}.");
        }

        if (!inspection.Columns.Contains(RequiredRevenueColumn, StringComparer.Ordinal))
        {
            return new VendorSalesNivelacijaContractIssue(
                RelationName,
                "column",
                inspection.ResolvedSchema,
                RequiredRevenueColumn,
                null,
                "vendor_sales_nivelacija_contract_missing",
                $"Pre/post nivelacija nije dostupna: nedostaje kolona {RequiredRevenueColumn} u relaciji {inspection.ResolvedSchema}.{RelationName}.");
        }

        return null;
    }
}
