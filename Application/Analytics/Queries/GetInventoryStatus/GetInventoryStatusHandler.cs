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
            var result = await _db.ProductsDim.AsNoTracking().GroupBy(_ => 1).Select(g => new
            {
                TotalSkuCount = g.Count(),
                // Sum ignores null quantities; do not coalesce null → 0 into on-hand.
                TotalOnHand = g.Sum(x => x.Kolicina > 0 ? x.Kolicina : (int?)0) ?? 0,
                // Known positive quantity at/below threshold only.
                LowStockCount = g.Count(x => x.Kolicina != null && x.Kolicina > 0 && x.Kolicina <= request.LowStockThreshold),
                // Measured zero only — null quantity is not OOS.
                OutOfStockCount = g.Count(x => x.Kolicina == 0)
            }).FirstOrDefaultAsync(cancellationToken);

            return new InventoryStatusDto(
                TotalSkuCount: result?.TotalSkuCount ?? 0,
                TotalOnHand: result?.TotalOnHand ?? 0,
                LowStockCount: result?.LowStockCount ?? 0,
                OutOfStockCount: result?.OutOfStockCount ?? 0
            );
        }
    }
}
