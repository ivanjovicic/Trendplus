using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Application.Analytics.Queries.GetTopProducts
{
    public class GetTopProductsHandler : IRequestHandler<GetTopProductsQuery, TopProductsResult>
    {
        private readonly IAnalyticsDbContext _db;
        private readonly ILogger<GetTopProductsHandler> _logger;

        public GetTopProductsHandler(IAnalyticsDbContext db, ILogger<GetTopProductsHandler> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<TopProductsResult> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("GetTopProducts query: Top={Top}, FromDate={FromDate}, ToDate={ToDate}, StoreId={StoreId}",
                    request.Top, request.FromDate, request.ToDate, request.StoreId);

                var sales = _db.SalesFacts.AsNoTracking().AsQueryable();

                if (request.FromDate.HasValue)
                    sales = sales.Where(x => x.SaleTimestampUtc >= request.FromDate.Value);

                if (request.ToDate.HasValue)
                    sales = sales.Where(x => x.SaleTimestampUtc <= request.ToDate.Value);

                if (request.StoreId.HasValue)
                    sales = sales.Where(x => x.StoreId == request.StoreId.Value);

                var salesCount = await sales.CountAsync(cancellationToken);
                _logger.LogInformation("Sales count after filters: {Count}", salesCount);

                if (salesCount == 0)
                {
                    _logger.LogInformation("No sales found, returning empty lists");
                    return new TopProductsResult(new List<TopProductDto>(), new List<TopProductDto>());
                }

                var lines = _db.SalesLineFacts.AsNoTracking().AsQueryable();

                var joined = from l in lines
                             join s in sales on l.SaleId equals s.SaleId
                             join p in _db.ProductsDim.AsNoTracking() on l.ProductId equals p.ProductId into pjoin
                             from p in pjoin.DefaultIfEmpty()
                             select new
                             {
                                 l.ProductId,
                                 ProductName = p != null ? p.ProductName : string.Empty,
                                 Revenue = l.LineTotal,
                                 Units = l.Qty
                             };

                var grouped = joined
                    .GroupBy(x => new { x.ProductId, x.ProductName })
                    .Select(g => new TopProductDto(
                        g.Key.ProductId,
                        string.IsNullOrWhiteSpace(g.Key.ProductName) ? $"#{g.Key.ProductId}" : g.Key.ProductName,
                        g.Sum(x => x.Revenue),
                        g.Sum(x => x.Units)
                    ));

                var topByRevenue = await grouped
                    .OrderByDescending(x => x.TotalRevenue)
                    .Take(request.Top)
                    .ToListAsync(cancellationToken);

                var topByUnits = await grouped
                    .OrderByDescending(x => x.TotalUnits)
                    .Take(request.Top)
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
}
