using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Analytics.Queries.GetTopProducts;

public class GetTopProductsHandler : IRequestHandler<GetTopProductsQuery, TopProductsResult>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetTopProductsHandler> _logger;

    public GetTopProductsHandler(IAnalyticsDbContext db, ILogger<GetTopProductsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TopProductsResult> Handle(
        GetTopProductsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "GetTopProducts query: Top={Top}, FromDate={FromDate}, ToDate={ToDate}, StoreId={StoreId}",
                request.Top, request.FromDate, request.ToDate, request.StoreId);

            // 1. Filter SalesFacts first (KEEP BOTH: date filters commented out for debugging)
            var salesQuery = _db.SalesFacts.AsNoTracking();
            
            if (request.FromDate.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.SaleTimestampUtc >= request.FromDate.Value);
            }
            if (request.ToDate.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.SaleTimestampUtc <= request.ToDate.Value);
            }
            
            if (request.StoreId.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.StoreId == request.StoreId.Value);
            }

            var baseQuery = from sf in salesQuery
                            join slf in _db.SalesLineFacts.AsNoTracking() on sf.SaleId equals slf.SaleId
                            join p in _db.ProductsDim.AsNoTracking() on slf.ProductId equals p.ProductId into pj
                            from p in pj.DefaultIfEmpty()
                            group new { slf, p } by new { slf.ProductId, p.ProductName, p.Velicina, p.Boja } into g
                            select new
                            {
                                g.Key.ProductId,
                                ProductName = g.Key.ProductName ?? $"Product #{g.Key.ProductId}",
                                TotalRevenue = g.Sum(x => x.slf.LineTotal),
                                TotalUnits = g.Sum(x => x.slf.Qty),
                                g.Key.Velicina,
                                g.Key.Boja
                            };

            var topByRevenue = await baseQuery
                .OrderByDescending(x => x.TotalRevenue)
                .Take(request.Top)
                .Select(x => new TopProductDto(x.ProductId, x.ProductName, x.TotalRevenue, x.TotalUnits, x.Velicina, x.Boja))
                .ToListAsync(cancellationToken);

            var topByUnits = await baseQuery
                .OrderByDescending(x => x.TotalUnits)
                .Take(request.Top)
                .Select(x => new TopProductDto(x.ProductId, x.ProductName, x.TotalRevenue, x.TotalUnits, x.Velicina, x.Boja))
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Top products: {RevenueCount} by revenue, {UnitsCount} by units",
                topByRevenue.Count, topByUnits.Count);

            return new TopProductsResult(topByRevenue, topByUnits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTopProductsHandler");
            throw;
        }
    }
}
