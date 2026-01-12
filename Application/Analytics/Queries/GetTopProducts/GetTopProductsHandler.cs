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

            // 1. Filter SalesFacts first
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

            var salesCount = await salesQuery.CountAsync(cancellationToken);
            _logger.LogInformation("Sales count after filters: {SalesCount}", salesCount);

            // 2. Join filtered sales with SalesLineFacts
            var query = from sf in salesQuery
                        join slf in _db.SalesLineFacts.AsNoTracking() on sf.SaleId equals slf.SaleId
                        select slf;

            // 3. Group by ProductId and aggregate
            var aggregatedData = await query
                .GroupBy(slf => slf.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalRevenue = g.Sum(x => x.LineTotal),
                    TotalSold = g.Sum(x => x.Qty)
                })
                .ToListAsync(cancellationToken);

            // 4. Join with ProductsDim to get product details
            var productIds = aggregatedData.Select(a => a.ProductId).ToList();
            var products = await _db.ProductsDim
                .AsNoTracking()
                .Where(p => productIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId, p => p, cancellationToken);

            // 5. Materialize the final result
            var topProducts = aggregatedData.Select(a =>
            {
                var product = products.GetValueOrDefault(a.ProductId);
                return new TopProductDto(
                    a.ProductId,
                    product?.ProductName ?? $"Product #{a.ProductId}",
                    a.TotalRevenue,
                    a.TotalSold,
                    product?.Velicina,
                    product?.Boja
                );
            }).ToList();

            var topByRevenue = topProducts.OrderByDescending(p => p.TotalRevenue).Take(request.Top).ToList();
            var topByUnits = topProducts.OrderByDescending(p => p.TotalUnits).Take(request.Top).ToList();

            return new TopProductsResult(topByRevenue, topByUnits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTopProductsHandler");
            throw;
        }
    }
}
