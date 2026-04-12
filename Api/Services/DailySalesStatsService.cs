using System.Globalization;
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
    private static readonly string[] ExcludedDailySalesReceiptNumbers = ["DUG", "KOREKCIJA"];
    private static readonly string[] ExcludedDailySalesReceiptNumbersForQuery = ["DUG", "dug", "Dug", "KOREKCIJA", "korekcija", "Korekcija"];

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
        var saleTypeCandidates = TipPromeneConstants.ProdajaTypes.ToArray();
        var excludedReceiptNumbersForQuery = ExcludedDailySalesReceiptNumbersForQuery;

        var receiptHeaders = await _db.ProdajaZaglavlja
            .AsNoTracking()
            .Where(pz => pz.DatumProdaje >= fromDateUtc
                         && pz.DatumProdaje < toDateExclusiveUtc
                         && (!storeId.HasValue || pz.IDObjekat == storeId.Value))
            .Select(pz => new
            {
                SaleId = pz.Id,
                SaleDate = pz.DatumProdaje.Date,
                pz.BrojRacuna,
                pz.IDObjekat
            })
            .ToListAsync(ct);

        var includedReceiptHeaders = receiptHeaders
            .Where(x => !IsExcludedFromDailySales(x.BrojRacuna))
            .ToList();

        var duplicateReceiptGroups = includedReceiptHeaders
            .Where(x => !string.IsNullOrWhiteSpace(x.BrojRacuna))
            .GroupBy(x => new
            {
                x.SaleDate,
                x.BrojRacuna,
                x.IDObjekat
            })
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                g.Key.SaleDate,
                g.Key.BrojRacuna,
                g.Key.IDObjekat,
                HeaderCount = g.Count()
            })
            .OrderByDescending(x => x.HeaderCount)
            .ThenBy(x => x.SaleDate)
            .ToList();

        var receiptLineTotals = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            where pz.DatumProdaje >= fromDateUtc
               && pz.DatumProdaje < toDateExclusiveUtc
               && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
            group new
            {
                ps.Kolicina,
                ps.Cena
            } by new
            {
                SaleId = pz.Id,
                SaleDate = pz.DatumProdaje.Date,
                pz.BrojRacuna,
                pz.IDObjekat
            }
            into g
            select new
            {
                g.Key.SaleId,
                g.Key.SaleDate,
                g.Key.BrojRacuna,
                g.Key.IDObjekat,
                LineTotal = g.Sum(x => x.Kolicina * x.Cena)
            })
            .ToListAsync(ct);

        var dnevnikSaleTotals = await _db.DnevnikPromena
            .AsNoTracking()
            .Where(d => d.Datum >= fromDateUtc
                        && d.Datum < toDateExclusiveUtc
                        && (!storeId.HasValue || d.IDObjekat == storeId.Value)
                        && !excludedReceiptNumbersForQuery.Contains((d.BrojRacuna ?? string.Empty).Trim())
                        && saleTypeCandidates.Contains(d.TipPromene))
            .GroupBy(d => new
            {
                SaleId = d.Id,
                SaleDate = d.Datum.Date,
                d.BrojRacuna,
                d.IDObjekat
            })
            .Select(g => new
            {
                g.Key.SaleId,
                g.Key.SaleDate,
                g.Key.BrojRacuna,
                g.Key.IDObjekat,
                DnevnikTotal = g.Sum(x => x.Iznos < 0 ? -x.Iznos : x.Iznos)
            })
            .ToListAsync(ct);

        var dnevnikTotalsBySaleId = dnevnikSaleTotals.ToDictionary(x => x.SaleId);
        var receiptLineTotalsBySaleId = receiptLineTotals.ToDictionary(x => x.SaleId);
        var includedReceiptSaleIds = includedReceiptHeaders
            .Select(x => x.SaleId)
            .ToHashSet();

        var receiptAmountMismatches = receiptLineTotals
            .Where(x => includedReceiptSaleIds.Contains(x.SaleId))
            .Where(x => dnevnikTotalsBySaleId.TryGetValue(x.SaleId, out var dnevnik)
                        && decimal.Abs(x.LineTotal - dnevnik.DnevnikTotal) > 0.01m)
            .Select(x => new
            {
                x.SaleId,
                x.SaleDate,
                x.BrojRacuna,
                x.IDObjekat,
                x.LineTotal,
                DnevnikTotal = dnevnikTotalsBySaleId[x.SaleId].DnevnikTotal,
                Difference = decimal.Abs(x.LineTotal - dnevnikTotalsBySaleId[x.SaleId].DnevnikTotal)
            })
            .OrderByDescending(x => x.Difference)
            .ToList();

        var excludedReceiptHeaders = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            where pz.DatumProdaje >= fromDateUtc
               && pz.DatumProdaje < toDateExclusiveUtc
               && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
               && excludedReceiptNumbersForQuery.Contains((pz.BrojRacuna ?? string.Empty).Trim())
            group new
            {
                ps.Kolicina,
                ps.Cena
            } by new
            {
                SaleId = pz.Id,
                SaleDate = pz.DatumProdaje.Date,
                pz.BrojRacuna,
                pz.IDObjekat
            }
            into g
            select new
            {
                g.Key.SaleId,
                g.Key.SaleDate,
                g.Key.BrojRacuna,
                g.Key.IDObjekat,
                Revenue = g.Sum(x => x.Kolicina * x.Cena)
            })
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.SaleDate)
            .ToListAsync(ct);

        var nonStandardReceiptHeaders = includedReceiptHeaders
            .Where(x => !IsStandardReceiptNumber(x.BrojRacuna))
            .Select(x => new
            {
                x.SaleId,
                x.SaleDate,
                x.BrojRacuna,
                x.IDObjekat,
                Revenue = receiptLineTotalsBySaleId.TryGetValue(x.SaleId, out var lineTotal)
                    ? lineTotal.LineTotal
                    : dnevnikTotalsBySaleId.TryGetValue(x.SaleId, out var dnevnik)
                        ? dnevnik.DnevnikTotal
                        : 0m
            })
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.SaleDate)
            .ToList();

        var debtReceiptHeaders = nonStandardReceiptHeaders
            .Where(x => IsDebtReceiptNumber(x.BrojRacuna))
            .ToList();

        var excludedDebtReceiptHeaders = excludedReceiptHeaders
            .Where(x => IsDebtReceiptNumber(x.BrojRacuna))
            .ToList();

        var aggregates = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            join d in _db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into supplierJoin
            from supplier in supplierJoin.DefaultIfEmpty()
            where pz.DatumProdaje >= fromDateUtc
               && pz.DatumProdaje < toDateExclusiveUtc
               && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                    && !excludedReceiptNumbersForQuery.Contains((pz.BrojRacuna ?? string.Empty).Trim())
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

        if (duplicateReceiptGroups.Count > 0)
        {
            var sample = string.Join(
                ", ",
                duplicateReceiptGroups
                    .Take(3)
                    .Select(x => $"{x.BrojRacuna}/{x.IDObjekat ?? 0} ({x.HeaderCount}x)"));
            var suffix = duplicateReceiptGroups.Count > 3 ? " ..." : string.Empty;
            warnings.Add(
                $"Detektovano je {duplicateReceiptGroups.Count} grupa dupliranih racuna za isti datum/objekat. Primeri: {sample}{suffix}.");
        }

        if (excludedReceiptHeaders.Count > 0)
        {
            var sample = string.Join(
                ", ",
                excludedReceiptHeaders
                    .Take(3)
                    .Select(x => $"{(string.IsNullOrWhiteSpace(x.BrojRacuna) ? "(prazno)" : x.BrojRacuna)}/{x.IDObjekat ?? 0}"));
            var suffix = excludedReceiptHeaders.Count > 3 ? " ..." : string.Empty;
            warnings.Add(
                $"Iz dnevne prodaje su iskljucena {excludedReceiptHeaders.Count} dokumenta tipa DUG/korekcija u ukupnom iznosu od {decimal.Round(excludedReceiptHeaders.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero):0.##} RSD. Primeri: {sample}{suffix}.");
        }

        if (receiptAmountMismatches.Count > 0)
        {
            var sample = string.Join(
                ", ",
                receiptAmountMismatches
                    .Take(3)
                    .Select(x => $"{x.BrojRacuna ?? x.SaleId.ToString(CultureInfo.InvariantCulture)} ({x.LineTotal:0.##} vs {x.DnevnikTotal:0.##})"));
            var suffix = receiptAmountMismatches.Count > 3 ? " ..." : string.Empty;
            warnings.Add(
                $"Detektovano je {receiptAmountMismatches.Count} racuna gde zbir stavki ne odgovara dnevniku prodaje. Primeri: {sample}{suffix}.");
        }

        if (nonStandardReceiptHeaders.Count > 0)
        {
            var sample = string.Join(
                ", ",
                nonStandardReceiptHeaders
                    .Take(3)
                    .Select(x => $"{(string.IsNullOrWhiteSpace(x.BrojRacuna) ? "(prazno)" : x.BrojRacuna)}/{x.IDObjekat ?? 0}"));
            var suffix = nonStandardReceiptHeaders.Count > 3 ? " ..." : string.Empty;
            warnings.Add(
                $"Detektovano je {nonStandardReceiptHeaders.Count} prodajnih dokumenata sa nestandardnim brojem racuna. Promet tih dokumenata je {decimal.Round(nonStandardReceiptHeaders.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero):0.##} RSD. Primeri: {sample}{suffix}.");
        }

        if (excludedDebtReceiptHeaders.Count > 0)
        {
            warnings.Add(
            $"Dokumenti oznaceni kao DUG su iskljuceni iz dnevne prodaje {excludedDebtReceiptHeaders.Count} put(a) sa ukupno {decimal.Round(excludedDebtReceiptHeaders.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero):0.##} RSD.");
        }

        var hasClassifiedShiftRows = aggregates.Any(x => ResolveShift(x.HourOfDay) is 1 or 2);
        var hasAnyRows = aggregates.Any(x => x.Qty != 0);
        var useNoTimeDataFallback = !hasClassifiedShiftRows && hasAnyRows;

        if (useNoTimeDataFallback && aggregates.Count > 0)
        {
            var hourDistribution = aggregates
                .GroupBy(x => x.HourOfDay)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}h={g.Sum(x => x.Qty)}")
                .ToList();
            _logger.LogWarning(
                "Daily-sales: no shift-classifiable hours detected. Remapping all to shift 1. HourDistribution=[{Hours}] TotalRows={TotalRows}",
                string.Join(", ", hourDistribution),
                aggregates.Count);
        }

        var offShiftItems = 0;
        var offShiftRevenue = 0m;
        var totalItemsInRange = 0;
        var fallbackMappedItems = 0;
        var fallbackMappedRevenue = 0m;

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
            if (shift == 0 && useNoTimeDataFallback)
            {
                shift = 1;
                fallbackMappedItems += row.Qty;
                fallbackMappedRevenue += row.Revenue;
            }

            if (shift == 0)
            {
                // Data with timestamps outside shift hours (e.g. imported Access data at midnight)
                // is mapped to first shift so it still counts in daily totals and supplier breakdown.
                shift = 1;
                offShiftItems += row.Qty;
                offShiftRevenue += row.Revenue;
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

        if (useNoTimeDataFallback)
        {
            warnings.Add("Satnica prodaje nije dostupna; kolicine su mapirane u prvu smenu.");
            _logger.LogWarning(
                "Daily-sales fallback applied: midnight-only timestamps mapped to first shift. MappedItems={MappedItems} MappedRevenue={MappedRevenue}",
                fallbackMappedItems,
                fallbackMappedRevenue);
        }

        if (offShiftItems > 0)
        {
            warnings.Add($"Prodaja van smena (06-14 / 14-22) mapirana u prvu smenu: {offShiftItems} kom, {offShiftRevenue:N2} RSD.");
        }

        // When no items found in the requested range, query the overall available range so the
        // frontend can show a helpful "data available from X to Y" message.
        DateTime? minAvailableDate = null;
        DateTime? maxAvailableDate = null;
        if (totalItemsInRange == 0)
        {
            var availabilityQuery = _db.ProdajaZaglavlja.AsNoTracking();
            if (storeId.HasValue)
                availabilityQuery = availabilityQuery.Where(pz => pz.IDObjekat == storeId.Value);

            var minRaw = await availabilityQuery.MinAsync(pz => (DateTime?)pz.DatumProdaje, ct);
            var maxRaw = await availabilityQuery.MaxAsync(pz => (DateTime?)pz.DatumProdaje, ct);

            if (minRaw.HasValue)
            {
                minAvailableDate = DateTime.SpecifyKind(minRaw.Value.Date, DateTimeKind.Utc);
                maxAvailableDate = DateTime.SpecifyKind(maxRaw!.Value.Date, DateTimeKind.Utc);
                warnings.Add(
                    $"Nema podataka za izabrani period. Podaci su dostupni od {minAvailableDate.Value:yyyy-MM-dd} do {maxAvailableDate.Value:yyyy-MM-dd}.");
            }
            else
            {
                warnings.Add("Nema podataka o prodaji u bazi.");
            }
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
                DuplicateReceiptGroupCount = duplicateReceiptGroups.Count,
                DuplicateReceiptHeaderCount = duplicateReceiptGroups.Sum(x => Math.Max(0, x.HeaderCount - 1)),
                ReceiptAmountMismatchCount = receiptAmountMismatches.Count,
                ReceiptAmountMismatchRevenue = decimal.Round(receiptAmountMismatches.Sum(x => x.Difference), 2, MidpointRounding.AwayFromZero),
                NonStandardReceiptCount = nonStandardReceiptHeaders.Count,
                NonStandardReceiptRevenue = decimal.Round(nonStandardReceiptHeaders.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero),
                DebtReceiptCount = excludedDebtReceiptHeaders.Count,
                DebtReceiptRevenue = decimal.Round(excludedDebtReceiptHeaders.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero),
                MinAvailableDate = minAvailableDate,
                MaxAvailableDate = maxAvailableDate,
                Warnings = warnings
            }
        };

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Daily-sales generated. From={FromDate} To={ToDate} StoreId={StoreId} TopN={TopN} Rows={Rows} UniqueSuppliers={UniqueSuppliers} UnknownPct={UnknownPct}",
                response.RequestedFrom,
                response.RequestedTo,
                response.StoreId,
                response.TopN,
                response.DateRows.Count,
                response.Metadata.UniqueSuppliersInRange,
                response.Metadata.UnknownSupplierPct);
        }

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
            // If supplier name is missing, return empty string so UI can render a blank header
            return string.Empty;
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

    private static bool IsStandardReceiptNumber(string? brojRacuna)
    {
        if (string.IsNullOrWhiteSpace(brojRacuna))
            return false;

        foreach (var ch in brojRacuna.Trim())
        {
            if (!char.IsDigit(ch))
                return false;
        }

        return true;
    }

    private static bool IsDebtReceiptNumber(string? brojRacuna)
        => string.Equals(brojRacuna?.Trim(), "DUG", StringComparison.OrdinalIgnoreCase);

    private static bool IsCorrectionReceiptNumber(string? brojRacuna)
        => string.Equals(brojRacuna?.Trim(), "KOREKCIJA", StringComparison.OrdinalIgnoreCase);

    private static bool IsExcludedFromDailySales(string? brojRacuna)
        => IsDebtReceiptNumber(brojRacuna) || IsCorrectionReceiptNumber(brojRacuna);

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
