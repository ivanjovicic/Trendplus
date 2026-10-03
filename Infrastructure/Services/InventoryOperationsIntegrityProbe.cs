using System.Globalization;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Independently reconciles the current article inventory dimension and its bounded sales-line
/// mirror. It does not call InventoryEndpoints or reuse its aggregation helpers.
/// </summary>
public sealed class InventoryOperationsIntegrityProbe : IOperationsAnalyticsIntegrityFamilyProbe
{
    private readonly ITrendplusDbContext _trendDb;
    private readonly IAnalyticsDbContext _analyticsDb;

    public InventoryOperationsIntegrityProbe(
        ITrendplusDbContext trendDb,
        IAnalyticsDbContext analyticsDb)
    {
        _trendDb = trendDb;
        _analyticsDb = analyticsDb;
    }

    public string Family => OperationsAnalyticsIntegrityFamilies.Inventory;

    public async Task<OperationsAnalyticsIntegrityProbeResult> ProbeAsync(
        OperationsAnalyticsIntegrityProbeRequest request)
    {
        var ct = request.CancellationToken;
        if (request.MaxRows <= 0)
            return OperationsAnalyticsIntegrityProbeResult.Degraded("Inventory probe row bound is invalid.");

        var rowLimit = Math.Min(request.MaxRows, Math.Max(1, request.Definition.MaxRows));
        var windowDays = (request.ToUtc - request.FromUtc).TotalDays;
        if (request.FromUtc >= request.ToUtc
            || windowDays > Math.Max(1, request.Definition.MaxWindowDays))
        {
            return OperationsAnalyticsIntegrityProbeResult.Degraded(
                "Inventory probe window is invalid or exceeds its configured bound.");
        }

        var scope = NormalizeScope(request.DataScope);
        var products = await ApplyArticleScope(_trendDb.Artikli.AsNoTracking(), scope)
            .OrderBy(article => article.Id)
            .Take(rowLimit + 1)
            .Select(article => new InventoryProductRow(article.Id, article.Kolicina, article.DataOrigin))
            .ToListAsync(ct);
        if (products.Count > rowLimit)
            return OperationsAnalyticsIntegrityProbeResult.Degraded("Inventory article row bound was exceeded.");
        if (products.Count == 0)
            return OperationsAnalyticsIntegrityProbeResult.Unverified("No in-scope Inventory article rows are available; the probe did not certify an empty stock state.", 0);
        if (products.Any(product => !product.Quantity.HasValue))
            return OperationsAnalyticsIntegrityProbeResult.Unverified("One or more in-scope articles have unknown on-hand quantity; missing stock was not converted to zero.", products.Count);

        var productIds = products.Select(product => product.Id).ToArray();
        var dimensions = await ApplyProductDimensionScope(_analyticsDb.ProductsDim.AsNoTracking(), scope)
            .Where(product => productIds.Contains(product.ProductId))
            .OrderBy(product => product.ProductId)
            .Take(rowLimit + 1)
            .Select(product => new InventoryProductRow(product.ProductId, product.Kolicina, product.DataOrigin))
            .ToListAsync(ct);
        if (dimensions.Count > rowLimit)
            return OperationsAnalyticsIntegrityProbeResult.Degraded("Inventory product-dimension row bound was exceeded.");
        if (dimensions.Count == 0 || dimensions.Any(product => !product.Quantity.HasValue))
            return OperationsAnalyticsIntegrityProbeResult.Unverified("The independent product-dimension stock evidence is missing or incomplete.", products.Count + dimensions.Count);

        var expectedStock = products.Select(product => new OperationsAnalyticsBucketReconciliation.BucketValue(
            ProductKey(product.Id),
            0m,
            product.Quantity!.Value));
        var actualStock = dimensions.Select(product => new OperationsAnalyticsBucketReconciliation.BucketValue(
            ProductKey(product.Id),
            0m,
            product.Quantity!.Value));
        var deltas = OperationsAnalyticsBucketReconciliation.Compare("inventory_stock", expectedStock, actualStock);

        var sourceSales = await (
                from line in _trendDb.ProdajaStavke.AsNoTracking()
                join sale in _trendDb.ProdajaZaglavlja.AsNoTracking() on line.IdProdaja equals sale.Id
                where productIds.Contains(line.IdArtikal)
                      && sale.DatumProdaje >= request.FromUtc
                      && sale.DatumProdaje < request.ToUtc
                orderby sale.DatumProdaje, sale.Id, line.Id
                select new InventorySaleLineRow(line.IdArtikal, sale.IDObjekat, line.Kolicina))
            .Take(rowLimit + 1)
            .ToListAsync(ct);
        if (sourceSales.Count > rowLimit)
            return OperationsAnalyticsIntegrityProbeResult.Degraded("Inventory source sell-through row bound was exceeded.", products.Count);

        var mirroredSales = await (
                from line in _analyticsDb.SalesLineFacts.AsNoTracking()
                join sale in _analyticsDb.SalesFacts.AsNoTracking() on line.SaleId equals sale.SaleId
                where productIds.Contains(line.ProductId)
                      && sale.SaleTimestampUtc >= request.FromUtc
                      && sale.SaleTimestampUtc < request.ToUtc
                orderby sale.SaleTimestampUtc, sale.SaleId, line.Id
                select new InventorySaleLineRow(line.ProductId, (int?)sale.StoreId, line.Qty))
            .Take(rowLimit + 1)
            .ToListAsync(ct);
        if (mirroredSales.Count > rowLimit)
            return OperationsAnalyticsIntegrityProbeResult.Degraded("Inventory mirrored sell-through row bound was exceeded.", products.Count + sourceSales.Count);

        var rowCount = products.Count + dimensions.Count + sourceSales.Count + mirroredSales.Count;
        if (rowCount > rowLimit)
            return OperationsAnalyticsIntegrityProbeResult.Degraded("Inventory combined probe row bound was exceeded.", rowCount);
        if (sourceSales.Count == 0 || mirroredSales.Count == 0)
            return OperationsAnalyticsIntegrityProbeResult.Unverified("The requested Inventory sell-through window has no independent source and mirror rows to reconcile.", rowCount);

        var sourceBuckets = AggregateSales(sourceSales);
        var mirrorBuckets = AggregateSales(mirroredSales);
        deltas = deltas.Concat(OperationsAnalyticsBucketReconciliation.Compare(
            "inventory_sell_through",
            sourceBuckets,
            mirrorBuckets)).ToList();

        return deltas.Count > 0
            ? OperationsAnalyticsIntegrityProbeResult.DriftDetected(
                "Inventory stock identity/quantity or store-grain sell-through disagrees with its independent source.",
                deltas,
                rowCount)
            : OperationsAnalyticsIntegrityProbeResult.Verified(
                "Bounded Inventory article stock and store-grain sell-through reconcile across the operational and analytics sources.",
                probeRowCount: rowCount);
    }

    private static IQueryable<Domain.Model.Artikli> ApplyArticleScope(
        IQueryable<Domain.Model.Artikli> query,
        string scope)
        => scope switch
        {
            "imported" => query.Where(article => article.DataOrigin == "access"),
            "existing" => query.Where(article => article.DataOrigin == "existing" || article.DataOrigin == null || article.DataOrigin == ""),
            _ => query
        };

    private static IQueryable<Domain.Model.ProductsDim> ApplyProductDimensionScope(
        IQueryable<Domain.Model.ProductsDim> query,
        string scope)
        => scope switch
        {
            "imported" => query.Where(product => product.DataOrigin == "access"),
            "existing" => query.Where(product => product.DataOrigin == "existing" || product.DataOrigin == null || product.DataOrigin == ""),
            _ => query
        };

    private static string NormalizeScope(string? rawScope)
    {
        var scope = (rawScope ?? "all").Trim().ToLowerInvariant();
        return scope is "existing" or "imported" ? scope : "all";
    }

    private static string ProductKey(int productId)
        => "product:" + productId.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyList<OperationsAnalyticsBucketReconciliation.BucketValue> AggregateSales(
        IEnumerable<InventorySaleLineRow> rows)
        => rows.GroupBy(row => $"product:{row.ProductId.ToString(CultureInfo.InvariantCulture)}|store:{StoreKey(row.StoreId)}", StringComparer.Ordinal)
            .Select(group => new OperationsAnalyticsBucketReconciliation.BucketValue(
                group.Key,
                0m,
                group.Sum(row => row.Units)))
            .ToArray();

    private static string StoreKey(int? storeId)
        => storeId.HasValue ? storeId.Value.ToString(CultureInfo.InvariantCulture) : "unknown";

    private sealed record InventoryProductRow(int Id, int? Quantity, string? DataOrigin);
    private sealed record InventorySaleLineRow(int ProductId, int? StoreId, int Units);
}
