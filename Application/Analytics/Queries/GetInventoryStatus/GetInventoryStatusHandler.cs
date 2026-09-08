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
        private readonly ITrendplusDbContext _trendDb;

        public GetInventoryStatusHandler(IAnalyticsDbContext db, ITrendplusDbContext trendDb)
        {
            _db = db;
            _trendDb = trendDb;
        }

        public async Task<InventoryStatusDto> Handle(GetInventoryStatusQuery request, CancellationToken cancellationToken)
        {
            var query = _db.ProductsDim.AsNoTracking().AsQueryable();
            var normalizedDataScope = NormalizeDataScope(request.DataScope);

            if (request.FromDate.HasValue)
                query = query.Where(x => x.Timestamp >= request.FromDate.Value);
            if (request.ToDate.HasValue)
                query = query.Where(x => x.Timestamp <= request.ToDate.Value);
            if (normalizedDataScope != "all")
                query = query.Where(x => x.DataOrigin == normalizedDataScope);

            if (request.StoreId.HasValue || request.SupplierId.HasValue)
            {
                var scopedArticleIds = await _trendDb.Artikli
                    .AsNoTracking()
                    .Where(x => (!request.StoreId.HasValue || x.IDObjekat == request.StoreId.Value)
                        && (!request.SupplierId.HasValue || x.IDDobavljac == request.SupplierId.Value))
                    .Select(x => x.Id)
                    .ToArrayAsync(cancellationToken);

                query = query.Where(x => scopedArticleIds.Contains(x.ProductId));
            }

            var result = await query.GroupBy(_ => 1).Select(g => new
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

        private static string NormalizeDataScope(string? dataScope)
        {
            var normalized = (dataScope ?? "all").Trim().ToLowerInvariant();
            return normalized is "all" or "imported" or "existing" ? normalized : "all";
        }
    }
}
