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
                TotalOnHand = g.Sum(x => (int?)x.Kolicina) ?? 0,
                LowStockCount = g.Count(x => (x.Kolicina ?? 0) > 0 && (x.Kolicina ?? 0) <= request.LowStockThreshold),
                OutOfStockCount = g.Count(x => (x.Kolicina ?? 0) == 0)
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
