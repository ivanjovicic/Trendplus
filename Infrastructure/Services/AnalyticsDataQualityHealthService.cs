using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Infrastructure.Services;

public sealed class AnalyticsDataQualityHealthService
{
    /// <summary>
    /// Top-offender SQL contract (RQ06/RQ07).
    /// Article membership is scoped by <c>Artikli."DataOrigin"</c>;
    /// <c>sales_30d</c> revenue impact is scoped by sale-header <c>prodaja_zaglavlje.data_origin</c> (RQ05 sales-revenue rule).
    /// <c>missingCost</c> uses article nabavna cena (null/&lt;=0), independent of supplier/name CASE priority.
    /// </summary>
    public const string TopOffendersSql = """
            WITH sales_30d AS (
                SELECT
                    ps.id_artikal AS artikal_id,
                    COALESCE(SUM(ps.kolicina * ps.cena), 0) AS sales_30d
                FROM prodaja_stavke ps
                JOIN prodaja_zaglavlje p ON p.id = ps.id_prodaja
                WHERE p.datum_prodaje >= @salesFromUtc
                  AND (
                        @dataScope = 'all'
                     OR (@dataScope = 'imported' AND p.data_origin = 'access')
                     OR (@dataScope = 'existing' AND (p.data_origin = 'existing' OR p.data_origin IS NULL OR p.data_origin = ''))
                  )
                GROUP BY ps.id_artikal
            ),
            quality_source AS (
                SELECT
                    a."PLU" AS sku,
                    a."Id" AS product_id,
                    NULLIF(BTRIM(a."Naziv"), '') AS product_name,
                    NULLIF(BTRIM(d."Naziv"), '') AS supplier_name,
                    NULLIF(BTRIM(t."Naziv"), '') AS shoe_type_name,
                    CASE
                        WHEN a."IDDobavljac" IS NULL OR d."Id" IS NULL THEN 'missingSupplier'
                        WHEN a."IDTipObuce" IS NULL OR t."Id" IS NULL THEN 'missingShoeType'
                        WHEN NULLIF(BTRIM(a."Naziv"), '') IS NULL THEN 'invalidName'
                        ELSE 'ok'
                    END AS issue_type,
                    (a."NabavnaCena" IS NULL OR a."NabavnaCena" <= 0) AS is_missing_cost,
                    COALESCE(s.sales_30d, 0) AS sales_30d
                FROM "Artikli" a
                LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
                LEFT JOIN "TipoviObuce" t ON a."IDTipObuce" = t."Id"
                LEFT JOIN sales_30d s ON s.artikal_id = a."Id"
                WHERE @dataScope = 'all'
                   OR (@dataScope = 'imported' AND a."DataOrigin" = 'access')
                   OR (@dataScope = 'existing' AND (a."DataOrigin" = 'existing' OR a."DataOrigin" IS NULL OR a."DataOrigin" = ''))
            ),
            affected AS (
                SELECT
                    sku,
                    product_id,
                    product_name,
                    supplier_name,
                    shoe_type_name,
                    sales_30d AS revenue_impact_rsd
                FROM quality_source
                WHERE (
                        (@issueType = 'missingCost' AND is_missing_cost)
                     OR (@issueType <> 'missingCost' AND issue_type = @issueType)
                  )
                  AND sales_30d > @minSalesRsd
            ),
            totals AS (
                SELECT COALESCE(SUM(revenue_impact_rsd), 0) AS total_impact_rsd FROM affected
            )
            SELECT
                a.sku,
                a.product_id,
                a.product_name,
                a.supplier_name,
                a.shoe_type_name,
                a.revenue_impact_rsd AS sales_30d,
                a.revenue_impact_rsd,
                CASE
                    WHEN t.total_impact_rsd > 0 THEN ROUND((a.revenue_impact_rsd / t.total_impact_rsd * 100), 2)
                    ELSE 0
                END AS revenue_impact_pct,
                '/artikli/' || a.product_id || '/edit' AS action_url
            FROM affected a
            CROSS JOIN totals t
            ORDER BY a.revenue_impact_rsd DESC, a.product_id ASC
            LIMIT @limit;
            """;

    private readonly TrendplusDbContext _db;

    public AnalyticsDataQualityHealthService(TrendplusDbContext db)
    {
        _db = db;
    }

    public async Task<AnalyticsDataQualityHealthSnapshot> CaptureAsync(int lookbackDays, string? dataScope, CancellationToken ct)
    {
        var safeLookbackDays = Math.Max(1, lookbackDays);
        var windowToUtc = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
        var windowFromUtc = DateTime.UtcNow.Date.AddDays(-(safeLookbackDays - 1));
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        var orphanArticleCount = await (
            from a in _db.Artikli.AsNoTracking()
            join d in _db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dj
            from d in dj.DefaultIfEmpty()
            where a.IDDobavljac.HasValue && d == null
               && (!importedOnly || a.DataOrigin == "access")
               && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
            select a.Id)
            .CountAsync(ct);

        var salesWindow = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            join d in _db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dj
            from d in dj.DefaultIfEmpty()
            where pz.DatumProdaje >= windowFromUtc && pz.DatumProdaje <= windowToUtc
               && (!importedOnly || a.DataOrigin == "access")
               && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
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
            HasRevenueEvidence = totalRevenue > 0m,
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

    public async Task<IReadOnlyList<DataQualityTopOffenderDto>> GetTopOffendersAsync(
        string issueType,
        int limit,
        decimal minSalesRsd,
        string? dataScope,
        CancellationToken ct)
    {
        var normalizedIssueType = NormalizeTopOffenderIssueType(issueType);
        var normalizedDataScope = NormalizeDataScope(dataScope);

        var connection = _db.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Top offenders query requires an Npgsql connection.");

        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = new NpgsqlCommand(TopOffendersSql, connection);
            command.CommandTimeout = 60;
            command.Parameters.AddWithValue("salesFromUtc", DateTime.UtcNow.AddDays(-30));
            command.Parameters.AddWithValue("issueType", normalizedIssueType);
            command.Parameters.AddWithValue("minSalesRsd", minSalesRsd);
            command.Parameters.AddWithValue("limit", limit);
            command.Parameters.AddWithValue("dataScope", normalizedDataScope);

            var items = new List<DataQualityTopOffenderDto>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new DataQualityTopOffenderDto(
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.GetInt32(1).ToString(),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetDecimal(5),
                    reader.GetDecimal(6),
                    reader.IsDBNull(7) ? 0d : reader.GetDouble(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            }

            return items;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Normalizes top-offender issue types. Unknown values throw instead of silently becoming missingSupplier.
    /// </summary>
    public static string NormalizeTopOffenderIssueType(string? issueType)
    {
        var normalized = (issueType ?? string.Empty).Trim();
        return normalized switch
        {
            "missingSupplier" => "missingSupplier",
            "missingShoeType" => "missingShoeType",
            "invalidName" => "invalidName",
            "missingCost" => "missingCost",
            _ => throw new ArgumentOutOfRangeException(
                nameof(issueType),
                issueType,
                "Unsupported data-quality top-offender issue type.")
        };
    }

    private static string NormalizeDataScope(string? dataScope)
    {
        var normalized = (dataScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
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
    /// <summary>False when the lookback window has no sales revenue. Share-based health must not look green.</summary>
    public bool HasRevenueEvidence { get; set; }
    public decimal MissingCostRevenue { get; set; }
    public double MissingCostRevenueSharePct { get; set; }
    public decimal UnknownSupplierRevenue { get; set; }
    public double UnknownSupplierRevenueSharePct { get; set; }
}

public sealed record DataQualityTopOffenderDto(
    string? Sku,
    string ProductId,
    string? Name,
    string? SupplierName,
    string? ShoeTypeName,
    decimal Sales30d,
    decimal RevenueImpactRsd,
    double RevenueImpactPct,
    string? ActionUrl);
