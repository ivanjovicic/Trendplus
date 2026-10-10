using System.Globalization;
using Api.Models;
using Application.Analytics;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Trendplus2.Dtos;

namespace Api.Services;

public interface IDailySalesStatsService
{
    Task<DailySalesTableResponse> GetDailySalesAsync(
        DateTime requestedFromUtc,
        DateTime requestedToUtc,
        int? storeId,
        int topN,
        string? dataScope,
        CancellationToken ct = default,
        bool requestedToIsExclusive = false);
}

public sealed class DailySalesStatsService : IDailySalesStatsService
{
    private const string UtcInstantBasis = "utc_instant";
    private const string LegacyAccessWallClockBasis = "legacy_access_wall_clock";
    private const string DefaultShiftTimeZoneId = "UTC";
    private readonly TrendplusDbContext _db;
    private readonly ILogger<DailySalesStatsService> _logger;
    private readonly TimeZoneInfo _shiftTimeZone;
    private readonly string _shiftTimeZoneId;
    private readonly bool _shiftTimeZoneConfigurationWarning;

    public DailySalesStatsService(
        TrendplusDbContext db,
        ILogger<DailySalesStatsService> logger,
        IConfiguration? configuration = null)
    {
        _db = db;
        _logger = logger;
        var configuredTimeZoneId = configuration?["DailySales:TimeZoneId"];
        (_shiftTimeZone, _shiftTimeZoneId, _shiftTimeZoneConfigurationWarning) = ResolveShiftTimeZone(configuredTimeZoneId);
    }

    public async Task<DailySalesTableResponse> GetDailySalesAsync(
        DateTime requestedFromUtc,
        DateTime requestedToUtc,
        int? storeId,
        int topN,
        string? dataScope,
        CancellationToken ct = default,
        bool requestedToIsExclusive = false)
    {
        var normalizedScope = NormalizeDataScope(dataScope);
        var importedOnly = string.Equals(normalizedScope, "imported", StringComparison.Ordinal);
        var existingOnly = string.Equals(normalizedScope, "existing", StringComparison.Ordinal);

        var fromDateUtc = DateTime.SpecifyKind(requestedFromUtc.Date, DateTimeKind.Utc);
        // Callers pass a half-open end. The flag remains for source compatibility and no longer adds a day.
        _ = requestedToIsExclusive;
        var toDateExclusiveUtc = DateTime.SpecifyKind(requestedToUtc, DateTimeKind.Utc);
        var saleTypeCandidates = TipPromeneConstants.ProdajaTypes.ToArray();
        // The table population is line/article scoped. Reuse its receipt identity for
        // every receipt diagnostic so existing/imported views cannot inherit evidence
        // from the other population.
        var scopedSaleIdsQuery =
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja
                .Where(SalesReceiptPopulationPolicy.IncludedHeaderPredicate)
                .Where(SalesDataScopePolicy.HeaderPredicate(normalizedScope))
                .AsNoTracking() on ps.IdProdaja equals pz.Id
            where pz.DatumProdaje >= fromDateUtc
               && pz.DatumProdaje < toDateExclusiveUtc
               && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
            select pz.Id;

        var receiptHeaders = await _db.ProdajaZaglavlja
            .AsNoTracking()
            .Where(pz => pz.DatumProdaje >= fromDateUtc
                         && pz.DatumProdaje < toDateExclusiveUtc
                         && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                         && (!importedOnly && !existingOnly || scopedSaleIdsQuery.Contains(pz.Id)))
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

        var receiptLineTotals = (await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja
                .Where(SalesReceiptPopulationPolicy.IncludedHeaderPredicate)
                .Where(SalesDataScopePolicy.HeaderPredicate(normalizedScope))
                .AsNoTracking() on ps.IdProdaja equals pz.Id
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
            .ToListAsync(ct))
            .Select(x => new ReceiptLineTotalFact(
                x.SaleId,
                x.SaleDate,
                x.BrojRacuna,
                x.IDObjekat,
                x.LineTotal))
            .ToList();

        var scopedReceiptIdentities = includedReceiptHeaders
            .Select(x => ReceiptIdentityKeys.TryBuild(x.SaleDate, x.BrojRacuna, x.IDObjekat))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();
        var scopedMissingReceiptIdentities = includedReceiptHeaders
            .Where(x => string.IsNullOrWhiteSpace(x.BrojRacuna))
            .Select(x => (x.SaleDate, x.IDObjekat))
            .ToHashSet();

        var dnevnikFactsInRange = await _db.DnevnikPromena
            .AsNoTracking()
            .Where(d => d.Datum >= fromDateUtc
                        && d.Datum < toDateExclusiveUtc
                        && (!storeId.HasValue || d.IDObjekat == storeId.Value)
                        && !SalesReceiptPopulationPolicy.ExcludedReceiptNumbers.Contains((d.BrojRacuna ?? string.Empty).Trim().ToUpper())
                        && saleTypeCandidates.Contains(d.TipPromene))
            .Select(g => new
            {
                SaleId = g.Id,
                SaleDate = g.Datum.Date,
                g.BrojRacuna,
                g.IDObjekat,
                g.Iznos
            })
            .ToListAsync(ct);

        var dnevnikSaleFacts = dnevnikFactsInRange
            .Select(x => new DnevnikReceiptFact(
                x.SaleId,
                x.SaleDate,
                x.BrojRacuna,
                x.IDObjekat,
                x.Iznos))
            .Where(x =>
            {
                if (!importedOnly && !existingOnly)
                {
                    return true;
                }

                var identity = ReceiptIdentityKeys.TryBuild(x.SaleDate, x.BrojRacuna, x.IDObjekat);
                return identity.HasValue
                    ? scopedReceiptIdentities.Contains(identity.Value)
                    : scopedMissingReceiptIdentities.Contains((x.SaleDate, x.IDObjekat));
            })
            .ToList();

        var dnevnikTotalsByIdentity = dnevnikSaleFacts
            .Select(x => new
            {
                Identity = ReceiptIdentityKeys.TryBuild(x.SaleDate, x.BrojRacuna, x.IDObjekat),
                Amount = ReceiptIdentityKeys.NormalizeJournalSaleAmount(x.Iznos)
            })
            .Where(x => x.Identity.HasValue)
            .GroupBy(x => x.Identity!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var receiptLineTotalsBySaleId = receiptLineTotals.ToDictionary(x => x.SaleId);
        var includedReceiptSaleIds = includedReceiptHeaders
            .Select(x => x.SaleId)
            .ToHashSet();

        var receiptReconciliation = BuildReceiptReconciliation(
            receiptLineTotals,
            dnevnikSaleFacts,
            includedReceiptSaleIds);

        var excludedReceiptHeaders = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja
                .Where(SalesDataScopePolicy.HeaderPredicate(normalizedScope))
                .AsNoTracking() on ps.IdProdaja equals pz.Id
            where pz.DatumProdaje >= fromDateUtc
               && pz.DatumProdaje < toDateExclusiveUtc
               && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
               && SalesReceiptPopulationPolicy.ExcludedReceiptNumbers.Contains((pz.BrojRacuna ?? string.Empty).Trim().ToUpper())
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
            .Select(x =>
            {
                decimal? revenue = null;
                if (receiptLineTotalsBySaleId.TryGetValue(x.SaleId, out var lineTotal))
                {
                    revenue = lineTotal.LineTotal;
                }
                else
                {
                    var identity = ReceiptIdentityKeys.TryBuild(x.SaleDate, x.BrojRacuna, x.IDObjekat);
                    if (identity.HasValue && dnevnikTotalsByIdentity.TryGetValue(identity.Value, out var dnevnikTotal))
                    {
                        revenue = dnevnikTotal;
                    }
                }

                return new
                {
                    x.SaleId,
                    x.SaleDate,
                    x.BrojRacuna,
                    x.IDObjekat,
                    Revenue = revenue
                };
            })
            .OrderByDescending(x => x.Revenue ?? decimal.MinValue)
            .ThenBy(x => x.SaleDate)
            .ToList();
        var nonStandardReceiptsWithKnownRevenue = nonStandardReceiptHeaders
            .Where(x => x.Revenue.HasValue)
            .Select(x => new { x.SaleId, x.SaleDate, x.BrojRacuna, x.IDObjekat, Revenue = x.Revenue!.Value })
            .ToList();
        var nonStandardUnavailableCount = nonStandardReceiptHeaders.Count(x => !x.Revenue.HasValue);

        var debtReceiptHeaders = nonStandardReceiptsWithKnownRevenue
            .Where(x => string.Equals(x.BrojRacuna?.Trim(), "DUG", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var excludedDebtReceiptHeaders = excludedReceiptHeaders
            .Where(x => string.Equals(x.BrojRacuna?.Trim(), "DUG", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var aggregates = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja
                .Where(SalesReceiptPopulationPolicy.IncludedHeaderPredicate)
                .Where(SalesDataScopePolicy.HeaderPredicate(normalizedScope))
                .AsNoTracking() on ps.IdProdaja equals pz.Id
            // Supplier identity is frozen on the sale line. An absent or archived
            // article master row must never erase the historical sales line.
            join d in _db.Dobavljaci.AsNoTracking() on ps.SupplierIdAtSale equals d.Id into supplierJoin
            from supplier in supplierJoin.DefaultIfEmpty()
            where pz.DatumProdaje >= fromDateUtc
               && pz.DatumProdaje < toDateExclusiveUtc
               && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
            group new
            {
                ps.Kolicina,
                Revenue = ps.Kolicina * ps.Cena
            } by new
            {
                SaleDate = pz.DatumProdaje.Date,
                HourOfDay = pz.DatumProdaje.Hour,
                DataOrigin = pz.DataOrigin,
                SourceTimestampBasis = pz.SourceTimestampBasis,
                SupplierId = ps.SupplierIdAtSale,
                SupplierName = supplier != null ? supplier.Naziv : null,
                AttributionBasis = ps.AttributionBasis
            }
            into g
            select new SalesAggregateRow
            {
                SaleDate = DateTime.SpecifyKind(g.Key.SaleDate, DateTimeKind.Utc),
                HourOfDay = g.Key.HourOfDay,
                DataOrigin = g.Key.DataOrigin,
                SourceTimestampBasis = g.Key.SourceTimestampBasis,
                SupplierId = g.Key.SupplierId,
                SupplierName = g.Key.SupplierName,
                AttributionBasis = g.Key.AttributionBasis,
                LineCount = g.Count(),
                Qty = g.Sum(x => x.Kolicina),
                Revenue = g.Sum(x => x.Revenue)
            })
            .ToListAsync(ct);

        var attributionBases = aggregates
            .Select(x => string.IsNullOrWhiteSpace(x.AttributionBasis)
                ? SaleDimensionAttribution.Unknown
                : x.AttributionBasis!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var attributionLineCount = aggregates.Sum(x => x.LineCount);
        var attributedLineCount = aggregates
            .Where(x => !string.Equals(
                string.IsNullOrWhiteSpace(x.AttributionBasis) ? SaleDimensionAttribution.Unknown : x.AttributionBasis,
                SaleDimensionAttribution.Unknown,
                StringComparison.Ordinal))
            .Sum(x => x.LineCount);
        var attributionCoveragePct = attributionLineCount == 0
            ? (double?)null
            : Math.Round(attributedLineCount * 100d / attributionLineCount, 2);
        var attributionBasis = attributionBases.Length == 0
            ? null
            : attributionBases.Length == 1 ? attributionBases[0] : "mixed";

        var dayAccumulators = new Dictionary<DateTime, DayAccumulator>();
        var supplierTotals = new Dictionary<string, SupplierAccumulator>(StringComparer.Ordinal);
        var warnings = new List<string>();

        if (_shiftTimeZoneConfigurationWarning)
        {
            warnings.Add($"Konfigurisana vremenska zona za smene nije pronađena; korišćen je UTC ({DefaultShiftTimeZoneId}).");
        }

        if (duplicateReceiptGroups.Count > 0)
        {
            var sample = string.Join(
                ", ",
                duplicateReceiptGroups
                    .Take(3)
                    .Select(x => $"{x.BrojRacuna}/{x.IDObjekat ?? 0} ({x.HeaderCount}x)"));
            var suffix = duplicateReceiptGroups.Count > 3 ? " ..." : string.Empty;
            warnings.Add(
                $"Detektovano je {duplicateReceiptGroups.Count} grupa dupliranih računa za isti datum/objekat. Primeri: {sample}{suffix}.");
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
                $"Iz dnevne prodaje su isključena {excludedReceiptHeaders.Count} dokumenta tipa DUG/korekcija u ukupnom iznosu od {decimal.Round(excludedReceiptHeaders.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero):0.##} RSD. Primeri: {sample}{suffix}.");
        }

        if (receiptReconciliation.Status == "unverified")
        {
            warnings.Add(
                receiptReconciliation.ReasonCode == "no_matches"
                    ? "Nijedan račun nije podudaran sa dnevnikom prodaje; usklađenost nije potvrđena."
                    : "U izabranom periodu nema populacije računa za poređenje; usklađenost nije potvrđena.");
        }
        else if (receiptReconciliation.Status == "unavailable")
        {
            warnings.Add(
                "Dijagnostika neusklađenih računa nije dostupna jer identitet računa u dnevniku nema pouzdan broj računa; rezultat nije prikazan kao 0.");
        }
        else if (receiptReconciliation.Mismatches.Count > 0)
        {
            var sample = string.Join(
                ", ",
                receiptReconciliation.Mismatches
                    .Take(3)
                    .Select(x => $"{x.Identity.ReceiptNumber} ({x.LineTotal:0.##} vs {x.DnevnikTotal:0.##})"));
            var suffix = receiptReconciliation.Mismatches.Count > 3 ? " ..." : string.Empty;
            warnings.Add(
                $"Detektovano je {receiptReconciliation.Mismatches.Count} računa gde zbir stavki ne odgovara dnevniku prodaje. Primeri: {sample}{suffix}.");
        }

        if (nonStandardUnavailableCount > 0)
        {
            warnings.Add(
                $"Za {nonStandardUnavailableCount} nestandardnih dokumenata promet nije dostupan (nema ni zbir stavki ni podudaran dnevnik po broju računa); nije prikazan kao 0.");
        }

        if (nonStandardReceiptHeaders.Count > 0)
        {
            var knownRevenue = decimal.Round(nonStandardReceiptsWithKnownRevenue.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero);
            var sample = string.Join(
                ", ",
                nonStandardReceiptHeaders
                    .Take(3)
                    .Select(x => $"{(string.IsNullOrWhiteSpace(x.BrojRacuna) ? "(prazno)" : x.BrojRacuna)}/{x.IDObjekat ?? 0}"));
            var suffix = nonStandardReceiptHeaders.Count > 3 ? " ..." : string.Empty;
            warnings.Add(
                $"Detektovano je {nonStandardReceiptHeaders.Count} prodajnih dokumenata sa nestandardnim brojem računa. Poznat promet: {knownRevenue:0.##} RSD. Primeri: {sample}{suffix}.");
        }

        if (excludedDebtReceiptHeaders.Count > 0)
        {
            warnings.Add(
            $"Dokumenti označeni kao DUG su isključeni iz dnevne prodaje {excludedDebtReceiptHeaders.Count} put(a) sa ukupno {decimal.Round(excludedDebtReceiptHeaders.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero):0.##} RSD.");
        }

        var timestampFacts = aggregates
            .Select(x => new
            {
                Row = x,
                Basis = ResolveTimestampBasis(x.SourceTimestampBasis, x.DataOrigin)
            })
            .ToList();
        var knownTimestampRows = timestampFacts
            .Where(x => x.Basis is not null)
            .Sum(x => x.Row.LineCount);
        var unknownTimestampRows = timestampFacts
            .Where(x => x.Basis is null)
            .Sum(x => x.Row.LineCount);
        var unknownTimestampRevenue = timestampFacts
            .Where(x => x.Basis is null)
            .Sum(x => x.Row.Revenue);
        var timestampBases = timestampFacts
            .Where(x => x.Basis is not null)
            .Select(x => x.Basis!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var shiftTimestampBasis = unknownTimestampRows > 0
            ? timestampBases.Length > 0 ? "mixed" : "unknown"
            : timestampBases.Length switch
            {
                0 => "unknown",
                1 => timestampBases[0],
                _ => "mixed"
            };
        var hasClassifiedShiftRows = timestampFacts.Any(x =>
        {
            if (x.Basis is null) return false;
            var localTimestamp = ResolveLocalTimestamp(x.Row, x.Basis);
            return ResolveShift(localTimestamp.Hour) is 1 or 2;
        });
        var hasAnyKnownRows = timestampFacts.Any(x => x.Basis is not null && x.Row.Qty != 0);
        var hasSalesEvidence = aggregates.Count > 0;
        var useNoTimeDataFallback = !hasClassifiedShiftRows && hasAnyKnownRows && unknownTimestampRows == 0;

        if (useNoTimeDataFallback && aggregates.Count > 0)
        {
            var hourDistribution = aggregates
                .GroupBy(x => x.HourOfDay)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}h={g.Sum(x => x.Qty)}")
                .ToList();
            _logger.LogWarning(
                "Daily-sales: no shift-classifiable hours detected. Shift shares unavailable. HourDistribution=[{Hours}] TotalRows={TotalRows}",
                string.Join(", ", hourDistribution),
                aggregates.Count);
        }

        var offShiftItems = 0;
        var offShiftRevenue = 0m;
        var totalItemsInRange = 0;
        var noTimeFallbackItems = 0;
        var noTimeFallbackRevenue = 0m;

        foreach (var row in aggregates)
        {
            var supplierKey = BuildSupplierKey(row.SupplierId);
            if (!supplierTotals.TryGetValue(supplierKey, out var supplierAccumulator))
            {
                var resolved = ResolveSupplierIdentity(row.SupplierId, row.SupplierName);
                supplierAccumulator = new SupplierAccumulator
                {
                    SupplierId = resolved.SupplierId,
                    SupplierName = resolved.SupplierName,
                    IsUnknown = resolved.IsUnknown,
                    UnknownReason = resolved.UnknownReason
                };
                supplierAccumulator.MergeAttributionBasis(NormalizeAttributionBasis(row.AttributionBasis));
                supplierTotals[supplierKey] = supplierAccumulator;
            }
            else
            {
                supplierAccumulator.MergeAttributionBasis(NormalizeAttributionBasis(row.AttributionBasis));
            }

            // Daily totals and supplier buckets always include the row. Shift columns only
            // receive measured 06-14 / 14-22 hours — never remapped fallback or off-shift qty.
            var timestampBasis = ResolveTimestampBasis(row.SourceTimestampBasis, row.DataOrigin);
            var isUnknownTimestampBasis = timestampBasis is null;
            var localTimestamp = timestampBasis is null
                ? DateTime.SpecifyKind(row.SaleDate.Date.AddHours(row.HourOfDay), DateTimeKind.Utc)
                : ResolveLocalTimestamp(row, timestampBasis);
            var shift = isUnknownTimestampBasis ? 0 : ResolveShift(localTimestamp.Hour);
            // Keep unknown-basis lines in daily/supplier totals, but never invent a shift
            // from an unproven timestamp basis.
            if (!isUnknownTimestampBasis && shift == 0 && useNoTimeDataFallback)
            {
                noTimeFallbackItems += row.Qty;
                noTimeFallbackRevenue += row.Revenue;
            }
            else if (!isUnknownTimestampBasis && shift == 0)
            {
                offShiftItems += row.Qty;
                offShiftRevenue += row.Revenue;
            }

            supplierAccumulator.TotalQty += row.Qty;
            supplierAccumulator.TotalRevenue += row.Revenue;
            totalItemsInRange += row.Qty;

            var dateKey = DateTime.SpecifyKind(localTimestamp.Date, DateTimeKind.Utc);
            if (!dayAccumulators.TryGetValue(dateKey, out var day))
            {
                day = new DayAccumulator();
                dayAccumulators[dateKey] = day;
            }

            if (isUnknownTimestampBasis)
            {
                day.HasUnknownTimestampBasis = true;
            }

            if (shift == 1)
            {
                day.FirstShiftQty += row.Qty;
            }
            else if (shift == 2)
            {
                day.SecondShiftQty += row.Qty;
            }

            day.TotalItems += row.Qty;
            day.Revenue += row.Revenue;
            day.SupplierQty[supplierKey] = day.SupplierQty.GetValueOrDefault(supplierKey) + row.Qty;
        }

        var shiftAssignmentStatus = !hasSalesEvidence
            ? "unavailable"
            : useNoTimeDataFallback
                ? "no_time_fallback"
                : offShiftItems != 0 || unknownTimestampRows > 0
                    ? "partial"
                    : hasClassifiedShiftRows
                        ? "measured"
                        : "unavailable";

        var rankedSuppliers = supplierTotals
            .Select(x => new
            {
                Key = x.Key,
                Supplier = x.Value
            })
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
                UnknownReason = supplier.Supplier.UnknownReason,
                AttributionBasis = supplier.Supplier.AttributionBasis,
                TotalQty = supplier.Supplier.TotalQty,
                TotalRevenue = decimal.Round(supplier.Supplier.TotalRevenue, 2, MidpointRounding.AwayFromZero)
            });
        }

        if (duplicateNameLookup.Values.Any(x => x))
        {
            warnings.Add("Neki dobavljači imaju isti naziv; zaglavlja su razdvojena pomoću ID oznake.");
        }

        var rows = new List<DailySalesRowDto>();
        var requestedDateKeys = Enumerable
            .Range(0, (DateTime.SpecifyKind(toDateExclusiveUtc.AddTicks(-1).Date, DateTimeKind.Utc) - fromDateUtc.Date).Days + 1)
            .Select(offset => DateTime.SpecifyKind(fromDateUtc.Date.AddDays(offset), DateTimeKind.Utc));
        var rowDateKeys = requestedDateKeys
            .Concat(dayAccumulators.Keys)
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();
        foreach (var dateKey in rowDateKeys)
        {
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
            // The remainder is a signed net quantity. It can be negative when
            // returns/corrections outside top-N outweigh the omitted sales.
            var othersCount = totalItems - sumTop;

            int? firstShift = useNoTimeDataFallback || day?.HasUnknownTimestampBasis == true
                ? null
                : day?.FirstShiftQty ?? 0;
            int? secondShift = useNoTimeDataFallback || day?.HasUnknownTimestampBasis == true
                ? null
                : day?.SecondShiftQty ?? 0;

            rows.Add(new DailySalesRowDto
            {
                Date = dateKey,
                FirstShiftTotalItems = firstShift,
                SecondShiftTotalItems = secondShift,
                TotalRevenue = decimal.Round(day?.Revenue ?? 0m, 2, MidpointRounding.AwayFromZero),
                TopSupplierCounts = topCounts,
                OthersCount = othersCount,
                TotalItemsSold = totalItems
            });
        }

        var unknownSupplierItems = supplierTotals.Values
            .Where(x => x.IsUnknown)
            .Sum(x => x.TotalQty);

        decimal? unknownSupplierPct = totalItemsInRange > 0
            ? decimal.Round(unknownSupplierItems * 100m / totalItemsInRange, 2, MidpointRounding.AwayFromZero)
            : null;

        if (unknownSupplierPct is >= 20m)
        {
            warnings.Add("Veliki udeo prodaje ima nepoznatog dobavljača (20%+).");
        }

        if (useNoTimeDataFallback)
        {
            warnings.Add(
                $"Satnica prodaje nije dostupna za pouzdano razdvajanje smena; dnevni total ostaje vidljiv, a smenski udeo nije meren ({noTimeFallbackItems} kom, {noTimeFallbackRevenue:N2} RSD).");
            _logger.LogWarning(
                "Daily-sales no-time fallback: shift shares unavailable. MappedItems={MappedItems} MappedRevenue={MappedRevenue}",
                noTimeFallbackItems,
                noTimeFallbackRevenue);
        }

        if (offShiftItems != 0)
        {
            warnings.Add(
                $"Prodaja van smena (06-14 / 14-22) nije uključena u merene smene: {offShiftItems} kom, {offShiftRevenue:N2} RSD.");
        }

        if (unknownTimestampRows > 0)
        {
            warnings.Add(
                $"Za {unknownTimestampRows} redova nije potvrđena osnova vremena; uključeni su u dnevne totale, ali nisu dodeljeni smeni ({unknownTimestampRevenue:N2} RSD).");
        }

        // When no items found in the requested range, query the overall available range so the
        // frontend can show a helpful "data available from X to Y" message.
        DateTime? minAvailableDate = null;
        DateTime? maxAvailableDate = null;
        if (!hasSalesEvidence)
        {
            var availabilityQuery =
                from ps in _db.ProdajaStavke.AsNoTracking()
                join pz in _db.ProdajaZaglavlja
                    .Where(SalesReceiptPopulationPolicy.IncludedHeaderPredicate)
                    .Where(SalesDataScopePolicy.HeaderPredicate(normalizedScope))
                    .AsNoTracking() on ps.IdProdaja equals pz.Id
                where (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                select pz.DatumProdaje;

            var minRaw = await availabilityQuery.Select(date => (DateTime?)date).MinAsync(ct);
            var maxRaw = await availabilityQuery.Select(date => (DateTime?)date).MaxAsync(ct);

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

        var generatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var responseMeta = BuildDailySalesMeta(hasSalesEvidence, warnings, generatedAtUtc);
        responseMeta.RequestedPeriodFromUtc = fromDateUtc;
        responseMeta.RequestedPeriodToUtc = toDateExclusiveUtc;
        responseMeta.EffectivePeriodFromUtc = fromDateUtc;
        responseMeta.EffectivePeriodToUtc = toDateExclusiveUtc;
        responseMeta.RequestedDataScope = normalizedScope;
        responseMeta.EffectiveDataScope = normalizedScope;
        responseMeta.DataScopeSource = SalesDataScopePolicy.Source;
        responseMeta.AttributionBasis = attributionBasis;
        responseMeta.AttributionCoveragePct = attributionCoveragePct;

        var response = new DailySalesTableResponse
        {
            RequestedFrom = fromDateUtc,
            RequestedTo = toDateExclusiveUtc,
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
                UniqueSuppliersInRange = supplierTotals.Count,
                UnknownSupplierPct = unknownSupplierPct,
                UnknownSupplierItems = unknownSupplierItems,
                ShiftAssignmentStatus = shiftAssignmentStatus,
                ShiftTimeZone = _shiftTimeZoneId,
                ShiftTimestampBasis = shiftTimestampBasis,
                ShiftTimestampBasisKnownRows = knownTimestampRows,
                ShiftTimestampBasisUnknownRows = unknownTimestampRows,
                ShiftTimestampBasisUnknownRevenue = decimal.Round(unknownTimestampRevenue, 2, MidpointRounding.AwayFromZero),
                OffShiftItems = offShiftItems,
                OffShiftRevenue = decimal.Round(offShiftRevenue, 2, MidpointRounding.AwayFromZero),
                NoTimeFallbackItems = noTimeFallbackItems,
                NoTimeFallbackRevenue = decimal.Round(noTimeFallbackRevenue, 2, MidpointRounding.AwayFromZero),
                TotalItemsInRange = totalItemsInRange,
                DuplicateReceiptGroupCount = duplicateReceiptGroups.Count,
                DuplicateReceiptHeaderCount = duplicateReceiptGroups.Sum(x => Math.Max(0, x.HeaderCount - 1)),
                ReceiptAmountMismatchCount = receiptReconciliation.MismatchCount,
                ReceiptAmountMismatchRevenue = receiptReconciliation.MismatchAmount,
                ReceiptReconciliation = new DailySalesReceiptReconciliationDto
                {
                    Status = receiptReconciliation.Status,
                    ReasonCode = receiptReconciliation.ReasonCode,
                    MatchedReceiptCount = receiptReconciliation.MatchedReceiptCount,
                    UnmatchedReceiptCount = receiptReconciliation.UnmatchedReceiptCount,
                    UnmatchedDnevnikReceiptCount = receiptReconciliation.UnmatchedDnevnikReceiptCount,
                    MismatchCount = receiptReconciliation.MismatchCount,
                    MismatchAmount = receiptReconciliation.MismatchAmount
                },
                NonStandardReceiptCount = nonStandardReceiptHeaders.Count,
                NonStandardReceiptRevenue = decimal.Round(nonStandardReceiptsWithKnownRevenue.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero),
                DebtReceiptCount = excludedDebtReceiptHeaders.Count,
                DebtReceiptRevenue = decimal.Round(excludedDebtReceiptHeaders.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero),
                DiagnosticsDataScope = normalizedScope,
                AvailabilityDataScope = normalizedScope,
                MinAvailableDate = minAvailableDate,
                MaxAvailableDate = maxAvailableDate,
                Warnings = warnings
            },
            Meta = responseMeta
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

    private static AnalyticsResponseMetaDto BuildDailySalesMeta(
        bool hasSalesEvidence,
        IReadOnlyCollection<string> warnings,
        DateTime generatedAtUtc)
    {
        if (!hasSalesEvidence)
        {
            var emptyMeta = AnalyticsResponseMetaFactory.Empty("no_data_in_period", "Nema prodaje za izabrani period.");
            emptyMeta.GeneratedAtUtc = generatedAtUtc;
            return emptyMeta;
        }

        if (warnings.Count > 0)
        {
            var warningMeta = AnalyticsResponseMetaFactory.Warning(
                "DAILY_SALES_WARNINGS",
                "Dnevna prodaja ima upozorenja o kvalitetu podataka.",
                "warning");
            warningMeta.GeneratedAtUtc = generatedAtUtc;
            return warningMeta;
        }

        var successMeta = AnalyticsResponseMetaFactory.Success("good");
        successMeta.GeneratedAtUtc = generatedAtUtc;
        return successMeta;
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

    private static string? ResolveTimestampBasis(string? explicitBasis, string? dataOrigin)
    {
        if (string.Equals(explicitBasis, UtcInstantBasis, StringComparison.Ordinal)
            || string.Equals(explicitBasis, LegacyAccessWallClockBasis, StringComparison.Ordinal))
        {
            return explicitBasis;
        }

        if (string.Equals(dataOrigin, "access", StringComparison.OrdinalIgnoreCase))
            return LegacyAccessWallClockBasis;

        if (string.Equals(dataOrigin, "existing", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(dataOrigin))
        {
            return UtcInstantBasis;
        }

        return null;
    }

    private DateTime ResolveLocalTimestamp(SalesAggregateRow row, string timestampBasis)
    {
        var rawTimestamp = DateTime.SpecifyKind(
            row.SaleDate.Date.AddHours(row.HourOfDay),
            DateTimeKind.Unspecified);

        if (string.Equals(timestampBasis, LegacyAccessWallClockBasis, StringComparison.Ordinal))
        {
            // Access imported values are legacy local wall-clock values stamped as UTC
            // during import. Preserve their clock/date rather than applying an offset twice.
            return rawTimestamp;
        }

        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(rawTimestamp, DateTimeKind.Utc),
            _shiftTimeZone);
    }

    private static (TimeZoneInfo TimeZone, string Id, bool Warning) ResolveShiftTimeZone(string? configuredId)
    {
        if (string.IsNullOrWhiteSpace(configuredId))
            return (TimeZoneInfo.Utc, DefaultShiftTimeZoneId, false);

        var normalizedId = configuredId.Trim();
        try
        {
            return (TimeZoneInfo.FindSystemTimeZoneById(normalizedId), normalizedId, false);
        }
        catch (TimeZoneNotFoundException)
        {
            if (string.Equals(normalizedId, "Europe/Belgrade", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return (TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time"), normalizedId, false);
                }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
        }
        catch (InvalidTimeZoneException) { }

        return (TimeZoneInfo.Utc, DefaultShiftTimeZoneId, true);
    }

    private static string BuildSupplierKey(int? supplierId)
    {
        return supplierId.HasValue ? $"id:{supplierId.Value}" : "unknown";
    }

    private static ResolvedSupplierIdentity ResolveSupplierIdentity(int? supplierId, string? supplierName)
    {
        if (!supplierId.HasValue)
        {
            return new ResolvedSupplierIdentity(
                SupplierId: null,
                SupplierName: "Nepoznat dobavljač",
                IsUnknown: true,
                UnknownReason: "missing_attribution");
        }

        if (string.IsNullOrWhiteSpace(supplierName))
        {
            return new ResolvedSupplierIdentity(
                SupplierId: supplierId,
                SupplierName: $"Nepoznat dobavljač #{supplierId.Value}",
                IsUnknown: true,
                UnknownReason: "dangling_supplier_reference");
        }

        // Negative Access AutoNumber IDs and the archived placeholder (-999999999)
        // stay known whenever a Dobavljaci row exists.
        return new ResolvedSupplierIdentity(
            SupplierId: supplierId,
            SupplierName: supplierName.Trim(),
            IsUnknown: false,
            UnknownReason: null);
    }

    private static string NormalizeAttributionBasis(string? attributionBasis)
    {
        return string.IsNullOrWhiteSpace(attributionBasis)
            ? SaleDimensionAttribution.Unknown
            : attributionBasis.Trim();
    }

    private readonly record struct ResolvedSupplierIdentity(
        int? SupplierId,
        string SupplierName,
        bool IsUnknown,
        string? UnknownReason);

    private static string NormalizeNameForLookup(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// DnevnikPromena has no FK to ProdajaZaglavlje. The only stable cross-source
    /// identity available here is normalized receipt number + UTC calendar day + store.
    /// A missing receipt number therefore makes the diagnostic unavailable instead of
    /// treating coincident database IDs as a match.
    /// </summary>
    private static ReceiptReconciliationResult BuildReceiptReconciliation(
        IReadOnlyCollection<ReceiptLineTotalFact> receiptLineTotals,
        IReadOnlyCollection<DnevnikReceiptFact> dnevnikFacts,
        IReadOnlySet<int> includedReceiptSaleIds)
    {
        // Coverage decision (RQ438): verify over identity-bearing journal rows; rows without
        // BrojRacuna count toward unmatched dnevnik coverage instead of failing the whole period.
        var receiptTotalsByIdentity = receiptLineTotals
            .Where(x => includedReceiptSaleIds.Contains(x.SaleId))
            .Select(x => new { Identity = ReceiptIdentityKeys.TryBuild(x.SaleDate, x.BrojRacuna, x.IDObjekat), x.LineTotal })
            .Where(x => x.Identity.HasValue)
            .GroupBy(x => x.Identity!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.LineTotal));

        var dnevnikFactsWithIdentity = dnevnikFacts
            .Select(x => new
            {
                Fact = x,
                Identity = ReceiptIdentityKeys.TryBuild(x.SaleDate, x.BrojRacuna, x.IDObjekat),
                Amount = ReceiptIdentityKeys.NormalizeJournalSaleAmount(x.Iznos)
            })
            .ToList();
        var dnevnikTotalsByIdentity = dnevnikFactsWithIdentity
            .Where(x => x.Identity.HasValue)
            .GroupBy(x => x.Identity!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var matchedIdentities = receiptTotalsByIdentity.Keys
            .Intersect(dnevnikTotalsByIdentity.Keys)
            .ToHashSet();
        var unmatchedReceiptCount = receiptTotalsByIdentity.Keys.Count(x => !dnevnikTotalsByIdentity.ContainsKey(x));
        var missingDnevnikIdentityCount = dnevnikFactsWithIdentity.Count(x => !x.Identity.HasValue);
        var unmatchedDnevnikReceiptCount =
            dnevnikTotalsByIdentity.Keys.Count(x => !receiptTotalsByIdentity.ContainsKey(x))
            + missingDnevnikIdentityCount;

        if (dnevnikFacts.Count > 0 && dnevnikTotalsByIdentity.Count == 0)
        {
            return new ReceiptReconciliationResult(
                Status: "unavailable",
                ReasonCode: "dnevnik_receipt_identity_missing",
                MatchedReceiptCount: null,
                UnmatchedReceiptCount: unmatchedReceiptCount,
                UnmatchedDnevnikReceiptCount: unmatchedDnevnikReceiptCount,
                MismatchCount: null,
                MismatchAmount: null,
                Mismatches: []);
        }

        if (matchedIdentities.Count == 0)
        {
            var hasComparisonPopulation = receiptTotalsByIdentity.Count > 0
                || dnevnikTotalsByIdentity.Count > 0;
            return new ReceiptReconciliationResult(
                Status: "unverified",
                ReasonCode: hasComparisonPopulation ? "no_matches" : "empty_population",
                MatchedReceiptCount: 0,
                UnmatchedReceiptCount: unmatchedReceiptCount,
                UnmatchedDnevnikReceiptCount: unmatchedDnevnikReceiptCount,
                MismatchCount: 0,
                MismatchAmount: 0m,
                Mismatches: []);
        }

        var mismatches = matchedIdentities
            .Select(identity =>
            {
                var key = ToLocalIdentity(identity);
                return new ReceiptMismatchFact(
                    key,
                    receiptTotalsByIdentity[identity],
                    dnevnikTotalsByIdentity[identity],
                    decimal.Abs(receiptTotalsByIdentity[identity] - dnevnikTotalsByIdentity[identity]));
            })
            .Where(x => x.Difference > 0.01m)
            .OrderByDescending(x => x.Difference)
            .ToList();

        return new ReceiptReconciliationResult(
            Status: "verified",
            ReasonCode: missingDnevnikIdentityCount > 0 ? "partial_dnevnik_identity_coverage" : null,
            MatchedReceiptCount: matchedIdentities.Count,
            UnmatchedReceiptCount: unmatchedReceiptCount,
            UnmatchedDnevnikReceiptCount: unmatchedDnevnikReceiptCount,
            MismatchCount: mismatches.Count,
            MismatchAmount: decimal.Round(mismatches.Sum(x => x.Difference), 2, MidpointRounding.AwayFromZero),
            Mismatches: mismatches);
    }

    private static ReceiptIdentity ToLocalIdentity(ReceiptIdentityKeys.Key key)
        => new(key.SaleDateUtc, key.ReceiptNumberNormalized, key.StoreId);

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

    private static bool IsExcludedFromDailySales(string? brojRacuna)
        => SalesReceiptPopulationPolicy.IsExcluded(brojRacuna);

    private sealed class SalesAggregateRow
    {
        public DateTime SaleDate { get; init; }
        public int HourOfDay { get; init; }
        public string? DataOrigin { get; init; }
        public string? SourceTimestampBasis { get; init; }
        public int? SupplierId { get; init; }
        public string? SupplierName { get; init; }
        public string? AttributionBasis { get; init; }
        public int LineCount { get; init; }
        public int Qty { get; init; }
        public decimal Revenue { get; init; }
    }

    private sealed class SupplierAccumulator
    {
        public int? SupplierId { get; init; }
        public string SupplierName { get; init; } = "Nepoznat dobavljač";
        public bool IsUnknown { get; init; }
        public string? UnknownReason { get; init; }
        public string? AttributionBasis { get; private set; }
        public int TotalQty { get; set; }
        public decimal TotalRevenue { get; set; }

        public void MergeAttributionBasis(string nextBasis)
        {
            if (string.IsNullOrWhiteSpace(AttributionBasis))
            {
                AttributionBasis = nextBasis;
                return;
            }

            if (!string.Equals(AttributionBasis, nextBasis, StringComparison.Ordinal))
            {
                AttributionBasis = "mixed";
            }
        }
    }

    private sealed class DayAccumulator
    {
        public int FirstShiftQty { get; set; }
        public int SecondShiftQty { get; set; }
        public int TotalItems { get; set; }
        public decimal Revenue { get; set; }
        public bool HasUnknownTimestampBasis { get; set; }
        public Dictionary<string, int> SupplierQty { get; } = new(StringComparer.Ordinal);
    }

    private sealed record ReceiptLineTotalFact(
        int SaleId,
        DateTime SaleDate,
        string? BrojRacuna,
        int? IDObjekat,
        decimal LineTotal);

    private sealed record DnevnikReceiptFact(
        int SaleId,
        DateTime SaleDate,
        string? BrojRacuna,
        int? IDObjekat,
        decimal Iznos);


    private readonly record struct ReceiptIdentity(DateTime SaleDate, string ReceiptNumber, int? StoreId);

    private sealed record ReceiptMismatchFact(
        ReceiptIdentity Identity,
        decimal LineTotal,
        decimal DnevnikTotal,
        decimal Difference);

    private sealed record ReceiptReconciliationResult(
        string Status,
        string? ReasonCode,
        int? MatchedReceiptCount,
        int? UnmatchedReceiptCount,
        int? UnmatchedDnevnikReceiptCount,
        int? MismatchCount,
        decimal? MismatchAmount,
        IReadOnlyList<ReceiptMismatchFact> Mismatches);
}
