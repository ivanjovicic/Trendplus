using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class AnalyticsDataQualityHealthService
{
    private readonly TrendplusDbContext _db;

    public AnalyticsDataQualityHealthService(TrendplusDbContext db)
    {
        _db = db;
    }

    public async Task<AnalyticsDataQualityHealthSnapshot> CaptureAsync(int lookbackDays, CancellationToken ct)
    {
        var safeLookbackDays = Math.Max(1, lookbackDays);
        var windowToUtc = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
        var windowFromUtc = DateTime.UtcNow.Date.AddDays(-(safeLookbackDays - 1));

        var orphanArticleCount = await (
            from a in _db.Artikli.AsNoTracking()
            join d in _db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dj
            from d in dj.DefaultIfEmpty()
            where a.IDDobavljac.HasValue && d == null
            select a.Id)
            .CountAsync(ct);

        var salesWindow = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            join d in _db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dj
            from d in dj.DefaultIfEmpty()
            where pz.DatumProdaje >= windowFromUtc && pz.DatumProdaje <= windowToUtc
            group new { ps, a, d } by 1 into g
            select new
            {
                TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                MissingCostRevenue = g.Sum(x => (x.ps.NabavnaCena ?? x.a.NabavnaCena).HasValue ? 0m : x.ps.Kolicina * x.ps.Cena),
                UnknownSupplierRevenue = g.Sum(x => !x.a.IDDobavljac.HasValue || x.d == null ? x.ps.Kolicina * x.ps.Cena : 0m)
            })
            .FirstOrDefaultAsync(ct);

        var totalRevenue = salesWindow?.TotalRevenue ?? 0m;
        var missingCostRevenue = salesWindow?.MissingCostRevenue ?? 0m;
        var unknownSupplierRevenue = salesWindow?.UnknownSupplierRevenue ?? 0m;

        return new AnalyticsDataQualityHealthSnapshot
        {
            GeneratedAtUtc = DateTime.UtcNow,
            LookbackDays = safeLookbackDays,
            WindowFromUtc = windowFromUtc,
            WindowToUtc = windowToUtc,
            OrphanArticleCount = orphanArticleCount,
            TotalRevenue = Math.Round(totalRevenue, 2),
            MissingCostRevenue = Math.Round(missingCostRevenue, 2),
            MissingCostRevenueSharePct = totalRevenue > 0m
                ? Math.Round((double)(missingCostRevenue / totalRevenue * 100m), 2)
                : 0d,
            UnknownSupplierRevenue = Math.Round(unknownSupplierRevenue, 2),
            UnknownSupplierRevenueSharePct = totalRevenue > 0m
                ? Math.Round((double)(unknownSupplierRevenue / totalRevenue * 100m), 2)
                : 0d
        };
    }
}

public sealed class AnalyticsDataQualityHealthSnapshot
{
    public DateTime GeneratedAtUtc { get; set; }
    public int LookbackDays { get; set; }
    public DateTime WindowFromUtc { get; set; }
    public DateTime WindowToUtc { get; set; }
    public int OrphanArticleCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal MissingCostRevenue { get; set; }
    public double MissingCostRevenueSharePct { get; set; }
    public decimal UnknownSupplierRevenue { get; set; }
    public double UnknownSupplierRevenueSharePct { get; set; }
}
