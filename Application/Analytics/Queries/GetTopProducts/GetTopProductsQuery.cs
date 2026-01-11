using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Analytics.Queries.GetTopProducts
{
    public record GetTopProductsQuery(
        DateTime? FromDate,
        DateTime? ToDate,
        int Top = 20,
        int? StoreId = null
    ) : IRequest<TopProductsResult>;

    public record TopProductDto(
        int ProductId,
        string ProductName,
        decimal TotalRevenue,
        int TotalUnits
    );

    public record TopProductsResult(
        List<TopProductDto> ByRevenue,
        List<TopProductDto> ByUnits
    );
}
