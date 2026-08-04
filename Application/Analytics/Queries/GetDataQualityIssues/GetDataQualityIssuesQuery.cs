using MediatR;

namespace Application.Analytics.Queries.GetDataQualityIssues;

public static class DataQualityIssueTypes
{
    public const string MissingSupplier = "missingSupplier";
    public const string MissingShoeType = "missingShoeType";
    public const string InvalidName = "invalidName";
    public const string MissingCost = "missingCost";

    /// <summary>
    /// Issue-list types. Unknown values still default to missingSupplier for backward compatibility
    /// of the issues list endpoint (RQ07 does not rewrite that handler).
    /// </summary>
    public static string Normalize(string? value)
    {
        return value switch
        {
            MissingSupplier => MissingSupplier,
            MissingShoeType => MissingShoeType,
            InvalidName => InvalidName,
            _ => MissingSupplier
        };
    }

    /// <summary>
    /// Top-offender types including missingCost. Returns false for unknown values (no silent fallback).
    /// </summary>
    public static bool TryNormalizeTopOffender(string? value, out string normalized)
    {
        normalized = (value ?? string.Empty).Trim();
        return normalized switch
        {
            MissingSupplier => true,
            MissingShoeType => true,
            InvalidName => true,
            MissingCost => true,
            _ => false
        };
    }
}

public sealed record GetDataQualityIssuesQuery(
    string? Type,
    int Page = 1,
    int PageSize = 25,
    string? Query = null,
    string? SortBy = null,
    string? SortDir = null,
    string? DataScope = null,
    decimal MinSalesRsd = 0m
) : IRequest<DataQualityIssueListDto>;

public sealed record DataQualityIssueListDto(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<DataQualityIssueItemDto> Items
);

public sealed record DataQualityIssueItemDto(
    string? Sku,
    string ProductId,
    string? Name,
    string? SupplierId,
    string? SupplierName,
    string? ShoeTypeId,
    string? ShoeTypeName,
    string IssueType,
    decimal Sales30d,
    int Stock,
    DateTime LastUpdated
);
