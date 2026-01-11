using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Analytics.Queries.GetInventoryStatus
{
    public class GetInventoryStatusHandler : IRequestHandler<GetInventoryStatusQuery, InventoryStatusDto>
    {
        private readonly IAnalyticsDbContext _db;

        public GetInventoryStatusHandler(IAnalyticsDbContext db)
        {
            _db = db;
        }

        public async Task<InventoryStatusDto> Handle(GetInventoryStatusQuery request, CancellationToken cancellationToken)
        {
            var q = _db.ProductsDim.AsNoTracking().AsQueryable();

            var totalSku = await q.CountAsync(cancellationToken);
            var totalOnHand = await q.SumAsync(x => (int?)x.Kolicina, cancellationToken) ?? 0;
            var outOfStock = await q.CountAsync(x => (x.Kolicina ?? 0) == 0, cancellationToken);
            var lowStock = await q.CountAsync(x => (x.Kolicina ?? 0) > 0 && (x.Kolicina ?? 0) <= request.LowStockThreshold, cancellationToken);

            return new InventoryStatusDto(
                TotalSkuCount: totalSku,
                TotalOnHand: totalOnHand,
                LowStockCount: lowStock,
                OutOfStockCount: outOfStock
            );
        }
    }
}
