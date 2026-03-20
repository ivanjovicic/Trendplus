using Microsoft.EntityFrameworkCore;
using Infrastructure.DbContexts;
using Application.Artikli.Common.Interfaces;
using Trendplus2.Dtos;

namespace Trendplus2.Endpoints;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/inventory/balance", async (
            ITrendplusDbContext db,
            int? storeId,
            int? supplierId,
            CancellationToken ct) =>
        {
            var query = db.Artikli.AsNoTracking().AsQueryable();

            if (storeId.HasValue)
                query = query.Where(a => a.IDObjekat == storeId.Value);

            if (supplierId.HasValue)
                query = query.Where(a => a.IDDobavljac == supplierId.Value);

            var totalSku = await query.CountAsync(ct);
            var totalOnHand = await query.SumAsync(a => (int?)a.Kolicina, ct) ?? 0;
            var lowStock = await query.CountAsync(a => (a.Kolicina ?? 0) <= (a.MinimalnaKolicina ?? 0) && (a.Kolicina ?? 0) > 0, ct);
            var outOfStock = await query.CountAsync(a => (a.Kolicina ?? 0) <= 0, ct);
            var estimatedValue = await query.SumAsync(a => (decimal?)( (a.NabavnaCena ?? 0m) * (a.Kolicina ?? 0) ), ct) ?? 0m;

            var dto = new InventoryBalanceDto(
                TotalSku: totalSku,
                TotalOnHand: totalOnHand,
                LowStockCount: lowStock,
                OutOfStockCount: outOfStock,
                EstimatedInventoryValue: Math.Round(estimatedValue, 2)
            );

            return Results.Ok(dto);
        })
        .WithName("GetInventoryBalance")
        .WithTags("Analytics");

        app.MapGet("/api/analytics/inventory/list", async (
            ITrendplusDbContext db,
            int page = 1,
            int pageSize = 50,
            int? storeId = null,
            int? supplierId = null,
            string? search = null,
            string? sortBy = null,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 1000);

            var query = db.Artikli.AsNoTracking().AsQueryable();

            if (storeId.HasValue)
                query = query.Where(a => a.IDObjekat == storeId.Value);
            if (supplierId.HasValue)
                query = query.Where(a => a.IDDobavljac == supplierId.Value);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => (a.Naziv ?? "").Contains(search) || (a.PLU ?? "").Contains(search));

            // simple sort
            query = sortBy?.ToLowerInvariant() switch
            {
                "kolicina" => query.OrderByDescending(a => a.Kolicina),
                "naziv" => query.OrderBy(a => a.Naziv),
                _ => query.OrderByDescending(a => (a.Kolicina ?? 0))
            };

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new InventoryListItemDto(
                    a.Id,
                    a.PLU,
                    a.Naziv,
                    a.Kolicina,
                    a.MinimalnaKolicina,
                    a.NabavnaCena,
                    (a.NabavnaCena ?? 0m) * (a.Kolicina ?? 0),
                    a.IDObjekat,
                    a.IDDobavljac
                ))
                .ToListAsync(ct);

            var paged = new ArtikliPagedResponse<InventoryListItemDto>(items, total, page, pageSize);
            return Results.Ok(paged);
        })
        .WithName("GetInventoryList")
        .WithTags("Analytics");
    }
}
