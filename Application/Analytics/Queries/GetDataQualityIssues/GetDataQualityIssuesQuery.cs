using MediatR;

namespace Application.Analytics.Queries.GetDataQualityIssues;

public static class DataQualityIssueTypes
{
    public const string MissingSupplier = "missingSupplier";
    public const string MissingShoeType = "missingShoeType";
    public const string InvalidName = "invalidName";

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
}

public sealed record GetDataQualityIssuesQuery(
    string? Type,
    int Page = 1,
    int PageSize = 25,
    string? Query = null,
    string? SortBy = null,
    string? SortDir = null
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
