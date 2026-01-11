using MediatR;
using System;

namespace Application.Analytics.Queries.GetSalesSummary
{
    public record GetSalesSummaryQuery(
        DateTime? FromDate,
        DateTime? ToDate,
        int? StoreId = null
    ) : IRequest<SalesSummaryDto>;

    public record SalesSummaryDto(
        decimal TotalRevenue,
        int TotalTransactions,
        int TotalUnits,
        decimal AvgBasketValue,
        decimal AvgItemPrice
    );
}
