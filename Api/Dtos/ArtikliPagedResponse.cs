namespace Trendplus2.Dtos;

public sealed record ArtikliPagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
