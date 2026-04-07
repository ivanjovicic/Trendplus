using Api.Models;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IDailySalesStatsService
{
    Task<DailySalesTableResponse> GetDailySalesAsync(
        DateTime requestedFromUtc,
        DateTime requestedToUtc,
        int? storeId,
        int topN,
        string? dataScope,
        CancellationToken ct = default);
}

public sealed class DailySalesStatsService : IDailySalesStatsService
{
    private readonly TrendplusDbContext _db;
    private readonly ILogger<DailySalesStatsService> _logger;

    public DailySalesStatsService(TrendplusDbContext db, ILogger<DailySalesStatsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DailySalesTableResponse> GetDailySalesAsync(
        DateTime requestedFromUtc,
        DateTime requestedToUtc,
        int? storeId,
        int topN,
        string? dataScope,
        CancellationToken ct = default)
    {
        var normalizedScope = NormalizeDataScope(dataScope);
        var importedOnly = string.Equals(normalizedScope, "imported", StringComparison.Ordinal);
        var existingOnly = string.Equals(normalizedScope, "existing", StringComparison.Ordinal);

        var fromDateUtc = DateTime.SpecifyKind(requestedFromUtc.Date, DateTimeKind.Utc);
        var toDateUtc = DateTime.SpecifyKind(requestedToUtc.Date, DateTimeKind.Utc);
        var toDateExclusiveUtc = toDateUtc.AddDays(1);

        var aggregates = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            join d in _db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into supplierJoin
            from supplier in supplierJoin.DefaultIfEmpty()
            where pz.DatumProdaje >= fromDateUtc
               && pz.DatumProdaje < toDateExclusiveUtc
               && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
               && (!importedOnly || a.DataOrigin == "access")
               && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
            group new
            {
                ps.Kolicina,
                Revenue = ps.Kolicina * ps.Cena
            } by new
            {
                SaleDate = pz.DatumProdaje.Date,
                HourOfDay = pz.DatumProdaje.Hour,
                SupplierId = a.IDDobavljac,
                SupplierName = supplier != null ? supplier.Naziv : null
            }
            into g
            select new SalesAggregateRow
            {
                SaleDate = DateTime.SpecifyKind(g.Key.SaleDate, DateTimeKind.Utc),
                HourOfDay = g.Key.HourOfDay,
                SupplierId = g.Key.SupplierId,
                SupplierName = g.Key.SupplierName,
                Qty = g.Sum(x => x.Kolicina),
                Revenue = g.Sum(x => x.Revenue)
            })
            .ToListAsync(ct);

        var dayAccumulators = new Dictionary<DateTime, DayAccumulator>();
        var supplierTotals = new Dictionary<string, SupplierAccumulator>(StringComparer.Ordinal);
        var warnings = new List<string>();

        var offShiftItems = 0;
        var offShiftRevenue = 0m;
        var totalItemsInRange = 0;

        foreach (var row in aggregates)
        {
            var supplierKey = BuildSupplierKey(row.SupplierId);
            if (!supplierTotals.TryGetValue(supplierKey, out var supplierAccumulator))
            {
                supplierAccumulator = new SupplierAccumulator
                {
                    SupplierId = row.SupplierId,
                    SupplierName = ResolveSupplierName(row.SupplierId, row.SupplierName),
                    IsUnknown = !row.SupplierId.HasValue
                };
                supplierTotals[supplierKey] = supplierAccumulator;
            }

            var shift = ResolveShift(row.HourOfDay);
            if (shift == 0)
            {
                offShiftItems += row.Qty;
                offShiftRevenue += row.Revenue;
                continue;
            }

            supplierAccumulator.TotalQty += row.Qty;
            supplierAccumulator.TotalRevenue += row.Revenue;
            totalItemsInRange += row.Qty;

            var dateKey = DateTime.SpecifyKind(row.SaleDate.Date, DateTimeKind.Utc);
            if (!dayAccumulators.TryGetValue(dateKey, out var day))
            {
                day = new DayAccumulator();
                dayAccumulators[dateKey] = day;
            }

            if (shift == 1)
            {
                day.FirstShiftQty += row.Qty;
            }
            else
            {
                day.SecondShiftQty += row.Qty;
            }

            day.TotalItems += row.Qty;
            day.Revenue += row.Revenue;
            day.SupplierQty[supplierKey] = day.SupplierQty.GetValueOrDefault(supplierKey) + row.Qty;
        }

        var rankedSuppliers = supplierTotals
            .Select(x => new
            {
                Key = x.Key,
                Supplier = x.Value
            })
            .Where(x => x.Supplier.TotalQty > 0)
            .OrderByDescending(x => x.Supplier.TotalQty)
            .ThenByDescending(x => x.Supplier.TotalRevenue)
            .ThenBy(x => x.Supplier.SupplierName, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .ToList();

        var duplicateNameLookup = rankedSuppliers
            .GroupBy(x => NormalizeNameForLookup(x.Supplier.SupplierName))
            .ToDictionary(
                g => g.Key,
                g => g.Count() > 1,
                StringComparer.OrdinalIgnoreCase);

        var topSupplierKeys = new List<string>(rankedSuppliers.Count);
        var topSupplierHeaders = new List<DailySalesSupplierHeaderDto>(rankedSuppliers.Count);

        foreach (var supplier in rankedSuppliers)
        {
            var duplicateName = duplicateNameLookup.GetValueOrDefault(
                NormalizeNameForLookup(supplier.Supplier.SupplierName));
            var headerName = BuildTopSupplierHeaderName(supplier.Supplier, duplicateName);
            topSupplierKeys.Add(supplier.Key);
            topSupplierHeaders.Add(new DailySalesSupplierHeaderDto
            {
                SupplierId = supplier.Supplier.SupplierId,
                SupplierName = headerName,
                IsUnknown = supplier.Supplier.IsUnknown,
                TotalQty = supplier.Supplier.TotalQty,
                TotalRevenue = decimal.Round(supplier.Supplier.TotalRevenue, 2, MidpointRounding.AwayFromZero)
            });
        }

        if (duplicateNameLookup.Values.Any(x => x))
        {
            warnings.Add("Neki dobavljaci imaju isti naziv; zaglavlja su razdvojena pomocu ID oznake.");
        }

        var rows = new List<DailySalesRowDto>();
        for (var cursor = fromDateUtc.Date; cursor <= toDateUtc.Date; cursor = cursor.AddDays(1))
        {
            var dateKey = DateTime.SpecifyKind(cursor, DateTimeKind.Utc);
            dayAccumulators.TryGetValue(dateKey, out var day);

            var topCounts = new List<int>(topSupplierKeys.Count);
            var sumTop = 0;
            foreach (var supplierKey in topSupplierKeys)
            {
                var qty = day?.SupplierQty.GetValueOrDefault(supplierKey) ?? 0;
                topCounts.Add(qty);
                sumTop += qty;
            }

            var totalItems = day?.TotalItems ?? 0;
            var othersCount = Math.Max(0, totalItems - sumTop);
            if (sumTop > totalItems)
            {
                warnings.Add($"Detektovana nekonzistentnost top/others sabiranja za datum {dateKey:yyyy-MM-dd}.");
            }

            rows.Add(new DailySalesRowDto
            {
                Date = dateKey,
                FirstShiftTotalItems = day?.FirstShiftQty ?? 0,
                SecondShiftTotalItems = day?.SecondShiftQty ?? 0,
                TotalRevenue = decimal.Round(day?.Revenue ?? 0m, 2, MidpointRounding.AwayFromZero),
                TopSupplierCounts = topCounts,
                OthersCount = othersCount,
                TotalItemsSold = totalItems
            });
        }

        var unknownSupplierItems = supplierTotals.Values
            .Where(x => x.IsUnknown)
            .Sum(x => x.TotalQty);

        var unknownSupplierPct = totalItemsInRange > 0
            ? decimal.Round(unknownSupplierItems * 100m / totalItemsInRange, 2, MidpointRounding.AwayFromZero)
            : 0m;

        if (unknownSupplierPct >= 20m)
        {
            warnings.Add("Veliki udeo prodaje ima nepoznatog dobavljaca (20%+).");
        }

        if (offShiftItems > 0)
        {
            warnings.Add("Prodaja van smena 06-14 i 14-22 nije ukljucena u tabelarne kolicine.");
        }

        var response = new DailySalesTableResponse
        {
            RequestedFrom = fromDateUtc,
            RequestedTo = toDateUtc,
            StoreId = storeId,
            TopN = topN,
            DataScope = normalizedScope,
            TopSuppliers = topSupplierHeaders,
            TopSuppliersOrder = topSupplierHeaders.Select(x => x.SupplierName).ToList(),
            DateRows = rows
                .OrderByDescending(x => x.Date)
                .ToList(),
            Metadata = new DailySalesMetadata
            {
                TotalDays = rows.Count,
                UniqueSuppliersInRange = supplierTotals.Values.Count(x => x.TotalQty > 0),
                UnknownSupplierPct = unknownSupplierPct,
                UnknownSupplierItems = unknownSupplierItems,
                OffShiftItems = offShiftItems,
                OffShiftRevenue = decimal.Round(offShiftRevenue, 2, MidpointRounding.AwayFromZero),
                TotalItemsInRange = totalItemsInRange,
                Warnings = warnings
            }
        };

        _logger.LogInformation(
            "Daily-sales generated. From={FromDate} To={ToDate} StoreId={StoreId} TopN={TopN} Rows={Rows} UniqueSuppliers={UniqueSuppliers} UnknownPct={UnknownPct}",
            response.RequestedFrom,
            response.RequestedTo,
            response.StoreId,
            response.TopN,
            response.DateRows.Count,
            response.Metadata.UniqueSuppliersInRange,
            response.Metadata.UnknownSupplierPct);

        return response;
    }

    private static string NormalizeDataScope(string? dataScope)
    {
        var normalized = (dataScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    private static int ResolveShift(int hourOfDay)
    {
        if (hourOfDay >= 6 && hourOfDay < 14) return 1;
        if (hourOfDay >= 14 && hourOfDay < 22) return 2;
        return 0;
    }

    private static string BuildSupplierKey(int? supplierId)
    {
        return supplierId.HasValue ? $"id:{supplierId.Value}" : "unknown";
    }

    private static string ResolveSupplierName(int? supplierId, string? supplierName)
    {
        if (!supplierId.HasValue)
        {
            return "Nepoznato";
        }

        if (string.IsNullOrWhiteSpace(supplierName))
        {
            return $"Dobavljac #{supplierId.Value}";
        }

        return supplierName.Trim();
    }

    private static string NormalizeNameForLookup(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string BuildTopSupplierHeaderName(SupplierAccumulator supplier, bool duplicateName)
    {
        if (!duplicateName)
        {
            return supplier.SupplierName;
        }

        if (supplier.SupplierId.HasValue)
        {
            return $"{supplier.SupplierName} #{supplier.SupplierId.Value}";
        }

        return $"{supplier.SupplierName} (unknown)";
    }

    private sealed class SalesAggregateRow
    {
        public DateTime SaleDate { get; init; }
        public int HourOfDay { get; init; }
        public int? SupplierId { get; init; }
        public string? SupplierName { get; init; }
        public int Qty { get; init; }
        public decimal Revenue { get; init; }
    }

    private sealed class SupplierAccumulator
    {
        public int? SupplierId { get; init; }
        public string SupplierName { get; init; } = "Nepoznato";
        public bool IsUnknown { get; init; }
        public int TotalQty { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    private sealed class DayAccumulator
    {
        public int FirstShiftQty { get; set; }
        public int SecondShiftQty { get; set; }
        public int TotalItems { get; set; }
        public decimal Revenue { get; set; }
        public Dictionary<string, int> SupplierQty { get; } = new(StringComparer.Ordinal);
    }
}
