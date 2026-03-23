using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Analytics.Queries.GetSalesSummary
{
    public class GetSalesSummaryHandler : IRequestHandler<GetSalesSummaryQuery, SalesSummaryDto>
    {
        private readonly IAnalyticsDbContext _db;

        public GetSalesSummaryHandler(IAnalyticsDbContext db)
        {
            _db = db;
        }

        public async Task<SalesSummaryDto> Handle(GetSalesSummaryQuery request, CancellationToken cancellationToken)
        {
            var q = _db.SalesFacts.AsNoTracking().AsQueryable();

            if (request.FromDate.HasValue)
                q = q.Where(x => x.SaleTimestampUtc >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                q = q.Where(x => x.SaleTimestampUtc <= request.ToDate.Value);

            if (request.StoreId.HasValue)
                q = q.Where(x => x.StoreId == request.StoreId.Value);

            var result = await q.GroupBy(_ => 1).Select(g => new {
                TotalRevenue = g.Sum(x => x.TotalAmount),
                TotalTransactions = g.Count(),
                TotalUnits = g.Sum(x => (int?)x.TotalUnits) ?? 0
            }).FirstOrDefaultAsync(cancellationToken);

            var totalRevenue = result?.TotalRevenue ?? 0m;
            var totalTransactions = result?.TotalTransactions ?? 0;
            var totalUnits = result?.TotalUnits ?? 0;

            var avgBasket = totalTransactions > 0
                ? Math.Round(totalRevenue / totalTransactions, 2)
                : 0m;
            var avgItem = totalUnits > 0
                ? Math.Round(totalRevenue / totalUnits, 2)
                : 0m;

            return new SalesSummaryDto(
                totalRevenue,
                totalTransactions,
                totalUnits,
                avgBasket,
                avgItem
            );
        }
    }
}
