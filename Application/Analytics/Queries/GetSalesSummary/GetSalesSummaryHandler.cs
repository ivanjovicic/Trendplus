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

            var totalRevenue = await q.SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
            var totalTransactions = await q.CountAsync(cancellationToken);
            var totalUnits = await q.SumAsync(x => (int?)x.TotalUnits, cancellationToken) ?? 0;

            var avgBasket = totalTransactions > 0 ? totalRevenue / totalTransactions : 0m;
            var avgItem = totalUnits > 0 ? totalRevenue / totalUnits : 0m;

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
