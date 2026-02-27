using Api.Models;
using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Trendplus2.Endpoints;

public static class PreNivelacijaPriorityEndpoints
{
    private sealed class SeasonLite
    {
        public string Naziv { get; init; } = "N/A";
        public DateTime DatumOd { get; init; }
        public DateTime DatumDo { get; init; }
    }

    public static void MapPreNivelacijaPriorityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/pre-nivelacija-prioriteti", async (
            TrendplusDbContext db,
            IPreNivelacijaScoringService scoring,
            int? supplierId = null,
            int? seasonId = null,
            int? footwearTypeId = null,
            int? stockMin = null,
            int? stockMax = null,
            int? noSaleDaysMin = null,
            decimal? minScore = null,
            decimal? marginFloor = null,
            CancellationToken ct = default) =>
        {
            var query =
                from dp in db.DnevnikPromena
                join a in db.Artikli on dp.ArtikalId equals a.Id into aj
                from a in aj.DefaultIfEmpty()
                where dp.TipPromene == "Nivelacija" || dp.TipPromene == "Nivelacija cena"
                select new { dp, a };

            if (supplierId.HasValue)
            {
                query = query.Where(x => (x.dp.DobavljacId ?? x.a.IDDobavljac) == supplierId.Value);
            }

            if (seasonId.HasValue)
            {
                query = query.Where(x => x.a != null && x.a.IDSezona == seasonId.Value);
            }

            if (footwearTypeId.HasValue)
            {
                query = query.Where(x => x.a != null && x.a.IDTipObuce == footwearTypeId.Value);
            }

            var results = await query
                .Select(x => new
                {
                    x.dp.ArtikalId,
                    x.dp.Datum,
                    x.dp.TipPromene,
                    x.dp.NovaProdajnaCena,
                    x.dp.StaraProdajnaCena,
                    DobavljacId = x.dp.DobavljacId ?? x.a.IDDobavljac,
                    SeasonId = x.a != null ? x.a.IDSezona : null,
                    FootwearTypeId = x.a != null ? x.a.IDTipObuce : null
                })
                .ToListAsync(ct);

            return Results.Ok(results);
        })
        .WithName("GetPreNivelacijaPrioriteti")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }

    private static string BuildFormulaDescription()
    {
        return "Pre-Nivelacija Score = 0.30*StockPressure + 0.25*VelocityRisk + 0.20*RecencyRisk + 0.10*MarkdownOpportunity + 0.10*MarginPotential + 0.05*SeasonRecencyBoost";
    }

    private static string ResolvePriorityBand(decimal score)
    {
        if (score >= 75m) return "high";
        if (score >= 55m) return "medium";
        return "low";
    }

    private static string ResolveSupplierName(int? supplierId, IReadOnlyDictionary<int, string> suppliers)
    {
        if (supplierId.HasValue && suppliers.TryGetValue(supplierId.Value, out var name) && !string.IsNullOrWhiteSpace(name))
            return name.Trim();
        return "N/A";
    }

    private static string ResolveSeasonName(int? seasonId, IReadOnlyDictionary<int, SeasonLite> seasons)
    {
        if (seasonId.HasValue && seasons.TryGetValue(seasonId.Value, out var sez) && !string.IsNullOrWhiteSpace(sez.Naziv))
            return sez.Naziv.Trim();
        return "N/A";
    }

    private static string ResolveFootwearType(int? typeId, IReadOnlyDictionary<int, string> tipovi)
    {
        if (typeId.HasValue && tipovi.TryGetValue(typeId.Value, out var t) && !string.IsNullOrWhiteSpace(t))
            return t.Trim();
        return "N/A";
    }

    private static decimal ResolveSeasonRecencyBoost(int? seasonId, IReadOnlyDictionary<int, SeasonLite> seasons, DateTime maxSaleDate)
    {
        if (!seasonId.HasValue || !seasons.TryGetValue(seasonId.Value, out var season))
            return 30m;

        var from = season.DatumOd;
        var to = season.DatumDo;
        var date = maxSaleDate.Date;
        if (date >= from.Date && date <= to.Date) return 100m;

        var minDiff = Math.Min(Math.Abs((date - from.Date).Days), Math.Abs((date - to.Date).Days));
        if (minDiff <= 60) return 60m;
        return 20m;
    }

    private static PreNivelacijaQueueItemDto ToQueueItem(PreNivelacijaSkuCandidateDto sku, DateTime dueDateUtc, string? status = null)
    {
        return new PreNivelacijaQueueItemDto
        {
            ArtikalId = sku.ArtikalId,
            Sku = sku.Sku,
            SupplierName = sku.SupplierName,
            PreNivelacijaScore = sku.PreNivelacijaScore,
            PriorityBand = sku.PriorityBand,
            Owner = "Unassigned",
            Status = status ?? "Unassigned",
            DueDateUtc = dueDateUtc
        };
    }

    private static List<PreNivelacijaAlertDto> BuildAlerts(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates,
        IReadOnlyList<PreNivelacijaSupplierActionDto> suppliers)
    {
        var alerts = new List<PreNivelacijaAlertDto>();

        foreach (var sku in candidates
                     .Where(x => x.DaysSinceLastSale > 120 && x.StockUnits > 8)
                     .Take(8))
        {
            alerts.Add(new PreNivelacijaAlertDto
            {
                Type = "NoSaleStockPressure",
                Severity = "critical",
                Message = $"{sku.Sku} ({sku.SupplierName}) nema prodaju {sku.DaysSinceLastSale} dana uz zalihu {sku.StockUnits}.",
                SupplierName = sku.SupplierName,
                ArtikalId = sku.ArtikalId
            });
        }

        foreach (var sku in candidates
                     .Where(x => x.MarkdownEvents >= 2 && x.Velocity180 < 0.03m)
                     .Take(8))
        {
            alerts.Add(new PreNivelacijaAlertDto
            {
                Type = "RepeatedMarkdownLowSellThrough",
                Severity = "warning",
                Message = $"{sku.Sku} ima ponovljene markdown-e ({sku.MarkdownEvents}) i nizak velocity ({sku.Velocity180:0.000}).",
                SupplierName = sku.SupplierName,
                ArtikalId = sku.ArtikalId
            });
        }

        foreach (var sup in suppliers
                     .Where(x => x.WeekOverWeekRiskDeltaPct > 20m && x.HighPrioritySkuCount >= 3)
                     .Take(5))
        {
            alerts.Add(new PreNivelacijaAlertDto
            {
                Type = "SupplierRiskClusterWoW",
                Severity = "warning",
                Message = $"{sup.SupplierName} ima rast rizika {sup.WeekOverWeekRiskDeltaPct:0.##}% WoW uz {sup.HighPrioritySkuCount} high-priority SKU.",
                SupplierName = sup.SupplierName
            });
        }

        return alerts;
    }
}
