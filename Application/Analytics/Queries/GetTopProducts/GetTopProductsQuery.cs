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
        int TotalUnits,
        string? Velicina = null,    // Veli?ina cipela
        string? Boja = null          // Boja cipela
    );

    public record TopProductsResult(
        List<TopProductDto> ByRevenue,
        List<TopProductDto> ByUnits
    );
}
