using Application.Artikli.Common.Interfaces;
using Domain.Model.Prodaja;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Resolves the latest included business sale date for a requested source scope.
/// Used by both Pilot Intake (RQ467) and undated analytics defaults (RQ570).
/// </summary>
public static class ObservedSalesHorizonResolver
{
    public sealed record DefaultPeriod(DateTime FromUtc, DateTime ToUtc, DateTime HorizonUtc);
    public sealed record ObservedSalesWindow(DateTime? FromDate, DateTime? ToDate);

    public static async Task<DateTime?> ResolveAsync(
        ITrendplusDbContext db,
        int? storeId,
        int? supplierId,
        string? dataScope,
        CancellationToken ct)
    {
        var window = await ResolveWindowAsync(db, storeId, supplierId, dataScope, ct);
        return window.ToDate;
    }

    public static async Task<ObservedSalesWindow> ResolveWindowAsync(
        ITrendplusDbContext db,
        int? storeId,
        int? supplierId,
        string? dataScope,
        CancellationToken ct)
    {
        var headers = db.ProdajaZaglavlja.AsNoTracking()
            .Where(SalesReceiptPopulationPolicy.IncludedHeaderPredicate)
            .Where(SalesDataScopePolicy.HeaderPredicate(dataScope));

        var businessDates =
            from header in headers
            join line in db.ProdajaStavke.AsNoTracking() on header.Id equals line.IdProdaja
            select new { header.DatumProdaje, header.IDObjekat, line.IdArtikal };

        if (storeId.HasValue)
            businessDates = businessDates.Where(row => row.IDObjekat == storeId.Value);

        if (supplierId.HasValue)
        {
            businessDates =
                from row in businessDates
                join article in db.Artikli.AsNoTracking() on row.IdArtikal equals article.Id
                where article.IDDobavljac == supplierId.Value
                select row;
        }

        var window = await businessDates
            .GroupBy(_ => 1)
            .Select(group => new ObservedSalesWindow(
                group.Min(row => (DateTime?)row.DatumProdaje),
                group.Max(row => (DateTime?)row.DatumProdaje)))
            .FirstOrDefaultAsync(ct);
        return window ?? new ObservedSalesWindow(null, null);
    }

    public static async Task<DefaultPeriod?> ResolveDefaultPeriodAsync(
        ITrendplusDbContext db,
        int? storeId,
        int? supplierId,
        string? dataScope,
        CancellationToken ct)
    {
        var horizon = await ResolveAsync(db, storeId, supplierId, dataScope, ct);
        if (!horizon.HasValue)
            return null;

        var horizonDate = horizon.Value.Date;
        return new DefaultPeriod(horizonDate.AddDays(-29), horizonDate.AddDays(1), horizonDate);
    }
}
