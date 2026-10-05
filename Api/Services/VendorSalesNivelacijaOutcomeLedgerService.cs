using Api.Models;
using Application.Analytics;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Api.Services;

internal static class VendorSalesNivelacijaOutcomeLedgerService
{
    private const int EventLimit = 5000;

    internal static async Task<VendorSalesNivelacijaOutcomeLedgerDto> LoadAsync(
        TrendplusDbContext db,
        int? vendorId,
        DateTime? eventDate,
        DateTime? from,
        DateTime? to,
        string? category,
        int? storeId,
        string normalizedDataScope,
        CancellationToken ct)
    {
        var events = new List<EventRow>();
        var connection = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(ct);
        try
        {
            const string sql = """
                SELECT e.price_event_id, e.event_date, e.store_id, e.article_id,
                       e.article_name, e.vendor_id, e.vendor_name, a."IDTipObuce",
                       COALESCE(t."Naziv", 'Nije dostupno'), e.discount_depth_pct
                FROM vw_vendor_sales_nivelacija e
                JOIN "DnevnikPromena" d ON d."Id" = e.price_event_id
                LEFT JOIN "Artikli" a ON a."Id" = e.article_id
                LEFT JOIN "TipoviObuce" t ON t."Id" = a."IDTipObuce"
                WHERE e.price_direction = 'markdown'
                  AND e.post_window_complete = TRUE
                  AND e.overlaps_next_event = FALSE
                  AND e.same_day_event_count = 1
                  AND (@vendor_id IS NULL OR e.vendor_id = @vendor_id)
                  AND (@event_date IS NULL OR e.event_date = @event_date)
                  AND (@from_date IS NULL OR e.event_date >= @from_date)
                  AND (@to_date IS NULL OR e.event_date <= @to_date)
                  AND (@store_id IS NULL OR e.store_id = @store_id)
                  AND (@category IS NULL OR e.category ILIKE @category_pattern)
                  AND (@data_scope = 'all'
                    OR (@data_scope = 'imported' AND d."DataOrigin" = 'access')
                    OR (@data_scope = 'existing' AND (d."DataOrigin" = 'existing' OR d."DataOrigin" IS NULL OR d."DataOrigin" = '')))
                ORDER BY e.event_date DESC, e.price_event_id DESC
                LIMIT @event_limit
                """;
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("vendor_id", NpgsqlDbType.Integer, (object?)vendorId ?? DBNull.Value);
            command.Parameters.AddWithValue("event_date", NpgsqlDbType.Date, (object?)eventDate?.Date ?? DBNull.Value);
            command.Parameters.AddWithValue("from_date", NpgsqlDbType.Date, (object?)from?.Date ?? DBNull.Value);
            command.Parameters.AddWithValue("to_date", NpgsqlDbType.Date, (object?)to?.Date ?? DBNull.Value);
            command.Parameters.AddWithValue("store_id", NpgsqlDbType.Integer, (object?)storeId ?? DBNull.Value);
            command.Parameters.AddWithValue("category", NpgsqlDbType.Text, (object?)category ?? DBNull.Value);
            command.Parameters.AddWithValue("category_pattern", NpgsqlDbType.Text, (object?)(category is null ? null : $"%{category}%") ?? DBNull.Value);
            command.Parameters.AddWithValue("data_scope", NpgsqlDbType.Text, normalizedDataScope);
            command.Parameters.AddWithValue("event_limit", NpgsqlDbType.Integer, EventLimit + 1);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                events.Add(new EventRow(
                    reader.GetInt64(0), DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.GetInt32(3),
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? "Nepoznato" : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetInt32(7), reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetDecimal(9)));
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }

        var isTruncated = events.Count > EventLimit;
        if (isTruncated) events.RemoveAt(events.Count - 1);

        var output = new List<VendorSalesNivelacijaOutcomeEventDto>(events.Count);
        if (events.Count > 0)
        {
            var articleIds = events.Select(e => e.ArticleId).Distinct().ToArray();
            var minDate = events.Min(e => e.Date).Date.AddDays(-30);
            var maxDate = events.Max(e => e.Date).Date.AddDays(30);
            var saleRows = await (
                from line in db.ProdajaStavke.AsNoTracking()
                join header in db.ProdajaZaglavlja.AsNoTracking() on line.IdProdaja equals header.Id
                join article in db.Artikli.AsNoTracking() on line.IdArtikal equals article.Id
                where articleIds.Contains(line.IdArtikal) && header.DatumProdaje >= minDate && header.DatumProdaje < maxDate
                    && (header.BrojRacuna == null || (header.BrojRacuna.Trim().ToUpper() != "DUG" && header.BrojRacuna.Trim().ToUpper() != "KOREKCIJA"))
                    && (normalizedDataScope == "all"
                        || (normalizedDataScope == "imported" && header.DataOrigin == "access")
                        || (normalizedDataScope == "existing" && (header.DataOrigin == "existing" || header.DataOrigin == null || header.DataOrigin == "")))
                select new SaleRow(line.Id, line.IdArtikal, header.DatumProdaje, header.IDObjekat,
                    line.Kolicina, line.Cena, line.NabavnaCena, article.NabavnaCenaDin, article.NabavnaCena)
            ).ToListAsync(ct);

            var activeBatchId = await db.AnalyticsCostSnapshotBatches.AsNoTracking()
                .Where(batch => batch.Status == "active" && batch.Scope == "access_origin")
                .OrderByDescending(batch => batch.ActivatedAtUtc).Select(batch => (long?)batch.Id).FirstOrDefaultAsync(ct);
            var snapshots = activeBatchId.HasValue
                ? await db.AnalyticsSaleLineCostSnapshots.AsNoTracking()
                    .Where(s => s.BatchId == activeBatchId && saleRows.Select(row => row.Id).Contains(s.ProdajaStavkaId))
                    .ToDictionaryAsync(s => s.ProdajaStavkaId, s => (decimal?)s.ResolvedUnitCost, ct)
                : new Dictionary<int, decimal?>();

            foreach (var e in events)
            {
                var pre = saleRows.Where(s => s.ArticleId == e.ArticleId && s.Date.Date >= e.Date.Date.AddDays(-30) && s.Date.Date < e.Date.Date
                    && (e.StoreId == null || s.StoreId == e.StoreId)).ToArray();
                var post = saleRows.Where(s => s.ArticleId == e.ArticleId && s.Date.Date >= e.Date.Date && s.Date.Date < e.Date.Date.AddDays(30)
                    && (e.StoreId == null || s.StoreId == e.StoreId)).ToArray();
                var preMetrics = Period(pre, snapshots, emptyWindowIsZero: false);
                var postMetrics = Period(post, snapshots, emptyWindowIsZero: true);
                output.Add(new VendorSalesNivelacijaOutcomeEventDto
                {
                    EventId = e.Id, EventDate = e.Date, StoreId = e.StoreId, ArticleId = e.ArticleId,
                    ArticleName = e.ArticleName, SupplierId = e.SupplierId, SupplierName = e.SupplierName,
                    ShoeTypeId = e.ShoeTypeId, ShoeType = e.ShoeType,
                    DiscountDepthPct = e.Depth, DepthBand = DepthBand(e.Depth),
                    PreUnits = preMetrics.Units, PostUnits = postMetrics.Units,
                    PreRevenue = preMetrics.Revenue, PostRevenue = postMetrics.Revenue,
                    HasComparableWindows = preMetrics.Units.HasValue && postMetrics.Units.HasValue
                        && preMetrics.Revenue.HasValue && postMetrics.Revenue.HasValue,
                    PreAveragePrice = preMetrics.Units > 0 && preMetrics.Revenue.HasValue ? preMetrics.Revenue / preMetrics.Units : null,
                    PostAveragePrice = postMetrics.Units > 0 && postMetrics.Revenue.HasValue ? postMetrics.Revenue / postMetrics.Units : null,
                    PreMarginContribution = preMetrics.Margin, PostMarginContribution = postMetrics.Margin,
                    PreCostCoveragePct = preMetrics.CoveragePct, PostCostCoveragePct = postMetrics.CoveragePct,
                    CostEvidenceReason = preMetrics.Margin.HasValue && postMetrics.Margin.HasValue
                        ? "sale_time_cost_fully_covered"
                        : preMetrics.CoveragePct.HasValue || postMetrics.CoveragePct.HasValue
                            ? "uncovered_sale_time_cost"
                            : "sale_cost_evidence_unavailable"
                });
            }
        }

        var aggregates = output.GroupBy(e => new { e.SupplierId, e.SupplierName, e.ShoeTypeId, e.ShoeType, e.DepthBand })
            .Select(group => new VendorSalesNivelacijaOutcomeAggregateDto
            {
                SupplierId = group.Key.SupplierId, SupplierName = group.Key.SupplierName,
                ShoeTypeId = group.Key.ShoeTypeId, ShoeType = group.Key.ShoeType, DepthBand = group.Key.DepthBand,
                EventCount = group.Count(), MatureComparableCount = group.Count(e => e.HasComparableWindows),
                MedianRevenueDelta = Median(group.Where(e => e.HasComparableWindows).Select(e => e.PostRevenue!.Value - e.PreRevenue!.Value)),
                MedianUnitsDelta = Median(group.Where(e => e.HasComparableWindows).Select(e => e.PostUnits!.Value - e.PreUnits!.Value)),
                MedianMarginDelta = Median(group.Where(e => e.PreMarginContribution.HasValue && e.PostMarginContribution.HasValue)
                    .Select(e => e.PostMarginContribution!.Value - e.PreMarginContribution!.Value)),
                CostCoveragePct = group.SelectMany(e => new[] { e.PreCostCoveragePct, e.PostCostCoveragePct })
                    .Where(x => x.HasValue).Select(x => (decimal?)x!.Value).Average()
            }).ToArray();

        return new VendorSalesNivelacijaOutcomeLedgerDto
        {
            EventCount = output.Count,
            MatureComparableCount = output.Count(e => e.HasComparableWindows),
            IsTruncated = isTruncated,
            EventLimit = EventLimit,
            Events = output,
            Aggregates = aggregates
        };
    }

    private static PeriodMetrics Period(IReadOnlyCollection<SaleRow> rows, IReadOnlyDictionary<int, decimal?> snapshots, bool emptyWindowIsZero)
    {
        var revenue = rows.Sum(row => row.Price * row.Quantity);
        if (rows.Count == 0) return emptyWindowIsZero
            ? new PeriodMetrics(0m, 0m, null, null)
            : new PeriodMetrics(null, null, null, null);
        decimal coveredRevenue = 0m;
        decimal margin = 0m;
        var allCovered = true;
        foreach (var row in rows)
        {
            snapshots.TryGetValue(row.Id, out var snapshot);
            var cost = AnalyticsMarginPolicy.ResolveUnitCostWithSnapshot(
                row.LineCost,
                snapshot,
                row.ProductCostRsd,
                row.ProductCostLegacy).UnitCost;
            if (cost is null) { allCovered = false; continue; }
            coveredRevenue += row.Price * row.Quantity;
            margin += (row.Price - cost.Value) * row.Quantity;
        }
        var coverage = revenue > 0 ? decimal.Round(coveredRevenue / revenue * 100m, 2) : (decimal?)null;
        return new PeriodMetrics(rows.Sum(row => (decimal)row.Quantity), revenue, coverage, allCovered ? margin : null);
    }

    private static string DepthBand(decimal? depth) => depth switch
    {
        null => "unavailable",
        < 10m => "under_10_pct",
        < 20m => "10_to_20_pct",
        < 30m => "20_to_30_pct",
        _ => "30_pct_or_more"
    };

    private static decimal? Median(IEnumerable<decimal> source)
    {
        var values = source.Order().ToArray();
        if (values.Length == 0) return null;
        var middle = values.Length / 2;
        return values.Length % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2m;
    }

    private sealed record EventRow(long Id, DateTime Date, int? StoreId, int ArticleId, string ArticleName,
        int? SupplierId, string SupplierName, int? ShoeTypeId, string ShoeType, decimal? Depth);
    private sealed record SaleRow(int Id, int ArticleId, DateTime Date, int? StoreId, int Quantity,
        decimal Price, decimal? LineCost, decimal? ProductCostRsd, decimal? ProductCostLegacy);
    private sealed record PeriodMetrics(decimal? Units, decimal? Revenue, decimal? CoveragePct, decimal? Margin);
}
