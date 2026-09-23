using Api.Models;
using Application.Analytics;
using Domain.Model;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

public interface IAnalyticsDetailReadService
{
    Task<AnalyticsDetailResponseDto?> GetDetailAsync(string table, string id, IQueryCollection query, CancellationToken ct = default);
}

public sealed class AnalyticsDetailReadService : IAnalyticsDetailReadService
{
    private sealed class AnalyticsFilters
    {
        public int? SezonaId { get; init; }
        public DateTime? RequestedFromUtc { get; init; }
        public DateTime? RequestedToUtc { get; init; }
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
        public int? StoreId { get; init; }
        public int? SupplierId { get; init; }
        public string? SezonaNaziv { get; init; }
        public string DataScope { get; init; } = "all";
    }

    private sealed class SalesRow
    {
        public int ArtikalId { get; init; }
        public int Kolicina { get; init; }
        public decimal Prihod { get; init; }
        public decimal? SaleLineCost { get; init; }
        public decimal? ProductCostRsd { get; init; }
        public decimal? ProductCostLegacy { get; init; }
        public DateTime DatumProdaje { get; init; }
        public string NazivArtikla { get; init; } = string.Empty;
        public string SifraArtikla { get; init; } = string.Empty;
        public int? DobavljacId { get; init; }
        public string DobavljacNaziv { get; init; } = "Nepoznato";
        public int? TipObuceId { get; init; }
        public string TipObuceNaziv { get; init; } = "Nepoznato";
        public string Boja { get; init; } = "Nepoznato";
    }

    private sealed class AnalyticsContext
    {
        public AnalyticsFilters Filters { get; init; } = new();
        public Dictionary<int, DateTime> PrvaNivelacijaPoArtiklu { get; init; } = [];
        public List<SalesRow> SalesRows { get; init; } = [];
        public Dictionary<int, decimal> ArticleSnapshotCosts { get; init; } = new Dictionary<int, decimal>();
        public bool IsSnapshotActive { get; init; }
        public DateTime? SnapshotGeneratedAtUtc { get; init; }
    }

    private sealed class ComparisonMetrics
    {
        public decimal? PreviousPeriodRevenue { get; init; }
        public int? PreviousPeriodUnits { get; init; }
        public double? PopRevenueChangePct { get; init; }
        public double? PopUnitsChangePct { get; init; }
    }

    private readonly TrendplusDbContext _db;
    private readonly IDnevnikPromenaReadService _dnevnikPromenaReadService;
    private readonly AnalyticsSnapshotOptions _snapshotOptions;

    public AnalyticsDetailReadService(
        TrendplusDbContext db,
        IDnevnikPromenaReadService dnevnikPromenaReadService,
        IOptions<AnalyticsSnapshotOptions> snapshotOptions)
    {
        _db = db;
        _dnevnikPromenaReadService = dnevnikPromenaReadService;
        _snapshotOptions = snapshotOptions.Value;
    }

    public async Task<AnalyticsDetailResponseDto?> GetDetailAsync(string table, string id, IQueryCollection query, CancellationToken ct = default)
    {
        var normalizedTable = (table ?? string.Empty).Trim().ToLowerInvariant();

        return normalizedTable switch
        {
            "dnevnik-promena" => await GetDnevnikPromenaDetailAsync(id, ct),
            "supplier-sales-stats" => await GetSupplierSalesDetailAsync(id, query, ct),
            "shoe-type-sales-stats" => await GetShoeTypeSalesDetailAsync(id, query, ct),
            "color-sales-stats" => await GetColorSalesDetailAsync(id, query, ct),
            "top-products" or "top-products-advanced" => await GetTopProductDetailAsync(id, query, ct),
            _ => null
        };
    }

    private async Task<AnalyticsDetailResponseDto?> GetDnevnikPromenaDetailAsync(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var movementId))
        {
            return null;
        }

        var detail = await _dnevnikPromenaReadService.GetByIdAsync(movementId, ct);
        if (detail is null)
        {
            return null;
        }

        return new AnalyticsDetailResponseDto
        {
            Table = "dnevnik-promena",
            RecordId = id,
            Title = detail.TipPromene,
            Subtitle = detail.NazivArtikla,
            Fields =
            [
                Field("datum", "Datum", detail.Datum.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture), "datetime"),
                Field("artikalId", "Artikal ID", detail.ArtikalId?.ToString(CultureInfo.InvariantCulture), "number"),
                Field("nazivArtikla", "Naziv artikla", detail.NazivArtikla, "text"),
                Field("kolicina", "Kolicina", detail.Kolicina?.ToString(CultureInfo.InvariantCulture), "number"),
                Field("staraCena", "Stara cena", detail.StaraCena?.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
                Field("novaCena", "Nova cena", detail.NovaCena?.ToString("0.00", CultureInfo.InvariantCulture), "currency", detail.StaraCena != detail.NovaCena),
                Field("iznos", "Iznos", detail.Iznos.ToString("0.00", CultureInfo.InvariantCulture), "currency", true),
                Field("brojRacuna", "Broj racuna", detail.BrojRacuna, "text"),
                Field("korisnikIme", "Korisnik", detail.KorisnikIme, "text"),
                Field("komentar", "Komentar", detail.Komentar, "text"),
                Field("dataOrigin", "Data origin", detail.DataOrigin, "text"),
                Field("sourceId", "Source ID", detail.SourceId.ToString(CultureInfo.InvariantCulture), "number")
            ]
        };
    }

    private async Task<AnalyticsDetailResponseDto?> GetSupplierSalesDetailAsync(string id, IQueryCollection query, CancellationToken ct)
    {
        var context = await BuildAnalyticsContextAsync(query, ct);
        List<SalesRow> rows;
        string title;

        if (int.TryParse(id, out var supplierId))
        {
            rows = context.SalesRows.Where(x => x.DobavljacId == supplierId).ToList();
            title = rows.FirstOrDefault()?.DobavljacNaziv ?? $"Dobavljac {id}";
        }
        else if ((id ?? string.Empty).StartsWith("unknown", StringComparison.OrdinalIgnoreCase))
        {
            rows = context.SalesRows
                .Where(x => !x.DobavljacId.HasValue || string.Equals(x.DobavljacNaziv, "Nepoznato", StringComparison.OrdinalIgnoreCase))
                .ToList();
            title = "Nepoznato";
        }
        else
        {
            return null;
        }

        var comparison = await GetSupplierComparisonMetricsAsync(
            id ?? string.Empty,
            context,
            rows.Sum(x => x.Prihod),
            rows.Sum(x => x.Kolicina),
            ct);

        return BuildAggregatedDetail("supplier-sales-stats", id ?? string.Empty, title, "Prodaja po dobavljacima", rows, context, comparison);
    }

    private async Task<AnalyticsDetailResponseDto?> GetShoeTypeSalesDetailAsync(string id, IQueryCollection query, CancellationToken ct)
    {
        var context = await BuildAnalyticsContextAsync(query, ct);
        var isUnknown = IsUnknownShoeTypeId(id);
        var knownId = int.TryParse(id, out var shoeTypeId) ? shoeTypeId : (int?)null;
        if (!knownId.HasValue && !isUnknown)
        {
            return null;
        }

        var rows = isUnknown
            ? context.SalesRows
                .Where(x => !x.TipObuceId.HasValue || string.Equals(x.TipObuceNaziv, "Nepoznato", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : context.SalesRows.Where(x => x.TipObuceId == knownId!.Value).ToList();
        if (rows.Count == 0)
        {
            return null;
        }

        var title = isUnknown
            ? "Nepoznato"
            : rows.FirstOrDefault()?.TipObuceNaziv ?? $"Tip obuće {knownId!.Value}";
        var comparison = await GetShoeTypeComparisonMetricsAsync(
            id ?? string.Empty,
            context,
            rows.Sum(x => x.Prihod),
            rows.Sum(x => x.Kolicina),
            ct);
        var aggregate = BuildAggregatedDetail(
            "shoe-type-sales-stats",
            id ?? string.Empty,
            title,
            "Prodaja po tipu obuće",
            rows,
            context,
            comparison);
        return aggregate is null ? null : BuildShoeTypeDetailProjection(aggregate, rows, context, comparison, isUnknown);
    }

    private async Task<AnalyticsDetailResponseDto?> GetColorSalesDetailAsync(string id, IQueryCollection query, CancellationToken ct)
    {
        var colorKey = Uri.UnescapeDataString(id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(colorKey))
        {
            return null;
        }

        var colorIdentityKey = ColorIdentityPolicy.Key(colorKey);
        var context = await BuildAnalyticsContextAsync(query, ct);
        var rows = context.SalesRows
            .Where(x => ColorIdentityPolicy.Key(x.Boja) == colorIdentityKey)
            .ToList();
        var title = rows
            .Select(x => x.Boja)
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? ColorIdentityPolicy.DisplayName(colorKey);
        var comparison = await GetColorComparisonMetricsAsync(
            colorIdentityKey,
            context,
            rows.Sum(x => x.Prihod),
            rows.Sum(x => x.Kolicina),
            ct);
        var aggregate = BuildAggregatedDetail(
            "color-sales-stats",
            colorIdentityKey,
            title,
            "Prodaja po boji artikla",
            rows,
            context,
            comparison,
            includeSnapshotCost: false);
        return aggregate is null ? null : BuildColorDetailProjection(aggregate, rows, context, comparison, colorIdentityKey);
    }

    private async Task<AnalyticsDetailResponseDto?> GetTopProductDetailAsync(string id, IQueryCollection query, CancellationToken ct)
    {
        if (!int.TryParse(id, out var artikalId))
        {
            return null;
        }

        var context = await BuildAnalyticsContextAsync(query, ct);
        var rows = context.SalesRows.Where(x => x.ArtikalId == artikalId).ToList();
        if (rows.Count == 0)
        {
            return null;
        }

        var article = await _db.Artikli.AsNoTracking()
            .Where(x => x.Id == artikalId)
            .Select(x => new
            {
                x.Id,
                x.Naziv,
                x.PLU,
                x.Boja
            })
            .FirstOrDefaultAsync(ct);

        var aggregate = BuildAggregatedDetail(
            "top-products",
            id,
            article?.Naziv?.Trim() ?? rows[0].NazivArtikla,
            "Detalj top proizvoda",
            rows,
            context);

        if (aggregate is null)
        {
            return null;
        }

        var fields = new List<AnalyticsDetailFieldDto>
        {
            Field("artikalId", "Artikal ID", artikalId.ToString(CultureInfo.InvariantCulture), "number"),
            Field("sifra", "SKU", article?.PLU?.Trim() ?? rows[0].SifraArtikla, "text"),
            Field("nazivArtikla", "Naziv artikla", article?.Naziv?.Trim() ?? rows[0].NazivArtikla, "text"),
            Field("boja", "Boja", ColorIdentityPolicy.DisplayName(article?.Boja ?? rows[0].Boja), "text")
        };

        fields.AddRange(aggregate.Fields);

        return new AnalyticsDetailResponseDto
        {
            Table = aggregate.Table,
            RecordId = aggregate.RecordId,
            Title = article?.Naziv?.Trim() ?? rows[0].NazivArtikla,
            Subtitle = article?.PLU?.Trim() ?? $"Artikal {artikalId}",
            Fields = fields,
            Metadata = aggregate.Metadata
        };
    }

    private async Task<AnalyticsContext> BuildAnalyticsContextAsync(IQueryCollection query, CancellationToken ct)
    {
        var filters = await ParseFiltersAsync(query, ct);

        var nivelacije = await _db.DnevnikPromena.AsNoTracking()
            .Where(d =>
                (d.TipPromene == TipPromeneConstants.Nivelacija || d.TipPromene == TipPromeneConstants.NivelacijaCena) &&
                d.ArtikalId.HasValue &&
                (!filters.ToUtc.HasValue || d.Datum <= filters.ToUtc.Value) &&
                (!filters.StoreId.HasValue || !d.IDObjekat.HasValue || d.IDObjekat == filters.StoreId.Value))
            .Select(d => new
            {
                ArtikalId = d.ArtikalId!.Value,
                DatumNivelacije = d.Datum
            })
            .ToListAsync(ct);

        var prvaNivelacijaPoArtiklu = nivelacije
            .GroupBy(n => n.ArtikalId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.DatumNivelacije));

        var importedOnly = string.Equals(filters.DataScope, "imported", StringComparison.OrdinalIgnoreCase);
        var existingOnly = string.Equals(filters.DataScope, "existing", StringComparison.OrdinalIgnoreCase);

        var salesRows = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            join d in _db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dj
            from d in dj.DefaultIfEmpty()
            join t in _db.TipoviObuce.AsNoTracking() on a.IDTipObuce equals t.Id into tj
            from t in tj.DefaultIfEmpty()
            where (!filters.FromUtc.HasValue || pz.DatumProdaje >= filters.FromUtc.Value)
               && (!filters.ToUtc.HasValue || pz.DatumProdaje <= filters.ToUtc.Value)
               && (!filters.StoreId.HasValue || pz.IDObjekat == filters.StoreId.Value)
               && (!importedOnly || a.DataOrigin == "access")
               && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
            select new SalesRow
            {
                ArtikalId = a.Id,
                Kolicina = ps.Kolicina,
                Prihod = ps.Kolicina * ps.Cena,
                SaleLineCost = ps.NabavnaCena,
                ProductCostRsd = a.NabavnaCenaDin,
                ProductCostLegacy = a.NabavnaCena,
                DatumProdaje = pz.DatumProdaje,
                NazivArtikla = a.Naziv ?? $"Artikal {a.Id}",
                SifraArtikla = a.PLU ?? a.Id.ToString(CultureInfo.InvariantCulture),
                DobavljacId = d != null ? d.Id : null,
                DobavljacNaziv = d != null && !string.IsNullOrWhiteSpace(d.Naziv) ? d.Naziv! : "Nepoznato",
                TipObuceId = t != null ? t.Id : null,
                TipObuceNaziv = t != null && !string.IsNullOrWhiteSpace(t.Naziv) ? t.Naziv : "Nepoznato",
                Boja = ColorIdentityPolicy.DisplayName(a.Boja)
            })
            .Where(x => !filters.SupplierId.HasValue || x.DobavljacId == filters.SupplierId.Value)
            .ToListAsync(ct);

        long? activeBatchId = null;
        DateTime? snapshotGeneratedAt = null;
        Dictionary<int, decimal> snapshotCostByArtikalId = [];
        if (_snapshotOptions.UseSnapshotCost)
        {
            var activeBatch = await _db.AnalyticsCostSnapshotBatches
                .Where(b => b.Status == "active" && b.Scope == "access_origin")
                .Select(b => new { b.Id, b.GeneratedAtUtc })
                .FirstOrDefaultAsync(ct);
            activeBatchId = activeBatch?.Id;
            snapshotGeneratedAt = activeBatch?.GeneratedAtUtc;
            if (activeBatchId.HasValue)
            {
                snapshotCostByArtikalId = await _db.AnalyticsSaleLineCostSnapshots
                    .Where(s => s.BatchId == activeBatchId.Value)
                    .GroupBy(s => s.ArtikalId)
                    .Select(g => new { ArtikalId = g.Key, Cost = g.Min(s => s.ResolvedUnitCost) })
                    .ToDictionaryAsync(x => x.ArtikalId, x => x.Cost, ct);
            }
        }

        return new AnalyticsContext
        {
            Filters = filters,
            PrvaNivelacijaPoArtiklu = prvaNivelacijaPoArtiklu,
            SalesRows = salesRows,
            ArticleSnapshotCosts = snapshotCostByArtikalId,
            IsSnapshotActive = activeBatchId.HasValue,
            SnapshotGeneratedAtUtc = snapshotGeneratedAt
        };
    }

    private async Task<ComparisonMetrics?> GetSupplierComparisonMetricsAsync(
        string? id,
        AnalyticsContext context,
        decimal currentRevenue,
        int currentUnits,
        CancellationToken ct)
    {
        var (previousFromUtc, previousToUtc) = BuildComparablePreviousRange(context.Filters.FromUtc, context.Filters.ToUtc);
        if (!previousFromUtc.HasValue || !previousToUtc.HasValue)
        {
            return null;
        }

        var importedOnly = string.Equals(context.Filters.DataScope, "imported", StringComparison.OrdinalIgnoreCase);
        var existingOnly = string.Equals(context.Filters.DataScope, "existing", StringComparison.OrdinalIgnoreCase);

        decimal previousRevenue;
        int previousUnits;

        if (int.TryParse(id, out var supplierId))
        {
            var aggregate = await (
                from ps in _db.ProdajaStavke.AsNoTracking()
                join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
                join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                where pz.DatumProdaje >= previousFromUtc.Value
                   && pz.DatumProdaje <= previousToUtc.Value
                   && (!context.Filters.StoreId.HasValue || pz.IDObjekat == context.Filters.StoreId.Value)
                   && a.IDDobavljac == supplierId
                   && (!importedOnly || a.DataOrigin == "access")
                   && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
                group ps by 1 into g
                select new
                {
                    Revenue = g.Sum(x => x.Kolicina * x.Cena),
                    Units = g.Sum(x => x.Kolicina)
                })
                .FirstOrDefaultAsync(ct);

            previousRevenue = aggregate?.Revenue ?? 0m;
            previousUnits = aggregate?.Units ?? 0;
        }
        else if ((id ?? string.Empty).StartsWith("unknown", StringComparison.OrdinalIgnoreCase))
        {
            var aggregate = await (
                from ps in _db.ProdajaStavke.AsNoTracking()
                join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
                join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                join d in _db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dj
                from d in dj.DefaultIfEmpty()
                where pz.DatumProdaje >= previousFromUtc.Value
                   && pz.DatumProdaje <= previousToUtc.Value
                   && (!context.Filters.StoreId.HasValue || pz.IDObjekat == context.Filters.StoreId.Value)
                   && (!a.IDDobavljac.HasValue || d == null || d.Naziv == null || d.Naziv.Trim() == "")
                   && (!importedOnly || a.DataOrigin == "access")
                   && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
                group ps by 1 into g
                select new
                {
                    Revenue = g.Sum(x => x.Kolicina * x.Cena),
                    Units = g.Sum(x => x.Kolicina)
                })
                .FirstOrDefaultAsync(ct);

            previousRevenue = aggregate?.Revenue ?? 0m;
            previousUnits = aggregate?.Units ?? 0;
        }
        else
        {
            return null;
        }

        return new ComparisonMetrics
        {
            PreviousPeriodRevenue = Math.Round(previousRevenue, 2),
            PreviousPeriodUnits = previousUnits,
            PopRevenueChangePct = previousRevenue > 0m
                ? Math.Round((double)((currentRevenue - previousRevenue) / previousRevenue * 100m), 2)
                : (double?)null,
            PopUnitsChangePct = previousUnits > 0
                ? Math.Round((currentUnits - previousUnits) / (double)previousUnits * 100d, 2)
                : (double?)null
        };
    }

    private async Task<ComparisonMetrics?> GetShoeTypeComparisonMetricsAsync(
        string? id,
        AnalyticsContext context,
        decimal currentRevenue,
        int currentUnits,
        CancellationToken ct)
    {
        var (previousFromUtc, previousToUtc) = BuildComparablePreviousRange(context.Filters.FromUtc, context.Filters.ToUtc);
        if (!previousFromUtc.HasValue || !previousToUtc.HasValue)
        {
            return null;
        }

        var importedOnly = string.Equals(context.Filters.DataScope, "imported", StringComparison.OrdinalIgnoreCase);
        var existingOnly = string.Equals(context.Filters.DataScope, "existing", StringComparison.OrdinalIgnoreCase);
        decimal previousRevenue;
        int previousUnits;

        if (int.TryParse(id, out var shoeTypeId))
        {
            var aggregate = await (
                from ps in _db.ProdajaStavke.AsNoTracking()
                join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
                join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                where pz.DatumProdaje >= previousFromUtc.Value
                   && pz.DatumProdaje <= previousToUtc.Value
                   && (!context.Filters.StoreId.HasValue || pz.IDObjekat == context.Filters.StoreId.Value)
                   && a.IDTipObuce == shoeTypeId
                   && (!importedOnly || a.DataOrigin == "access")
                   && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
                group ps by 1 into g
                select new
                {
                    Revenue = g.Sum(x => x.Kolicina * x.Cena),
                    Units = g.Sum(x => x.Kolicina)
                })
                .FirstOrDefaultAsync(ct);

            previousRevenue = aggregate?.Revenue ?? 0m;
            previousUnits = aggregate?.Units ?? 0;
        }
        else if (IsUnknownShoeTypeId(id))
        {
            var aggregate = await (
                from ps in _db.ProdajaStavke.AsNoTracking()
                join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
                join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                join t in _db.TipoviObuce.AsNoTracking() on a.IDTipObuce equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                where pz.DatumProdaje >= previousFromUtc.Value
                   && pz.DatumProdaje <= previousToUtc.Value
                   && (!context.Filters.StoreId.HasValue || pz.IDObjekat == context.Filters.StoreId.Value)
                   && (!a.IDTipObuce.HasValue || t == null || t.Naziv == null || t.Naziv.Trim() == "")
                   && (!importedOnly || a.DataOrigin == "access")
                   && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
                group ps by 1 into g
                select new
                {
                    Revenue = g.Sum(x => x.Kolicina * x.Cena),
                    Units = g.Sum(x => x.Kolicina)
                })
                .FirstOrDefaultAsync(ct);

            previousRevenue = aggregate?.Revenue ?? 0m;
            previousUnits = aggregate?.Units ?? 0;
        }
        else
        {
            return null;
        }

        return new ComparisonMetrics
        {
            PreviousPeriodRevenue = Math.Round(previousRevenue, 2),
            PreviousPeriodUnits = previousUnits,
            PopRevenueChangePct = previousRevenue > 0m
                ? Math.Round((double)((currentRevenue - previousRevenue) / previousRevenue * 100m), 2)
                : (double?)null,
            PopUnitsChangePct = previousUnits > 0
                ? Math.Round((currentUnits - previousUnits) / (double)previousUnits * 100d, 2)
                : (double?)null
        };
    }

    private async Task<ComparisonMetrics?> GetColorComparisonMetricsAsync(
        string colorIdentityKey,
        AnalyticsContext context,
        decimal currentRevenue,
        int currentUnits,
        CancellationToken ct)
    {
        var (previousFromUtc, previousToUtc) = BuildComparablePreviousRange(context.Filters.FromUtc, context.Filters.ToUtc);
        if (!previousFromUtc.HasValue || !previousToUtc.HasValue)
        {
            return null;
        }

        var importedOnly = string.Equals(context.Filters.DataScope, "imported", StringComparison.OrdinalIgnoreCase);
        var existingOnly = string.Equals(context.Filters.DataScope, "existing", StringComparison.OrdinalIgnoreCase);
        var previousRows = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            where pz.DatumProdaje >= previousFromUtc.Value
               && pz.DatumProdaje <= previousToUtc.Value
               && (!context.Filters.StoreId.HasValue || pz.IDObjekat == context.Filters.StoreId.Value)
               && (!context.Filters.SupplierId.HasValue || a.IDDobavljac == context.Filters.SupplierId.Value)
               && (!importedOnly || a.DataOrigin == "access")
               && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
            select new
            {
                Color = a.Boja,
                Revenue = ps.Kolicina * ps.Cena,
                Units = ps.Kolicina
            })
            .ToListAsync(ct);

        var matchingRows = previousRows
            .Where(x => ColorIdentityPolicy.Key(x.Color) == colorIdentityKey)
            .ToList();
        var previousRevenue = matchingRows.Sum(x => x.Revenue);
        var previousUnits = matchingRows.Sum(x => x.Units);

        return new ComparisonMetrics
        {
            PreviousPeriodRevenue = Math.Round(previousRevenue, 2),
            PreviousPeriodUnits = previousUnits,
            PopRevenueChangePct = previousRevenue > 0m
                ? Math.Round((double)((currentRevenue - previousRevenue) / previousRevenue * 100m), 2)
                : null,
            PopUnitsChangePct = previousUnits > 0
                ? Math.Round((currentUnits - previousUnits) / (double)previousUnits * 100d, 2)
                : null
        };
    }

    private async Task<AnalyticsFilters> ParseFiltersAsync(IQueryCollection query, CancellationToken ct)
    {
        var sezonaId = TryParseInt(query["sezonaId"]);
        var requestedFromUtc = NormalizeUtc(TryParseDateTime(query["fromDate"]));
        var requestedToUtc = NormalizeUtc(TryParseDateTime(query["toDate"]));
        var fromUtc = requestedFromUtc;
        var toUtc = ExpandInclusiveDateEnd(requestedToUtc);
        var storeId = TryParseInt(query["storeId"]);
        var supplierId = TryParseInt(query["supplierId"]);
        var dataScope = NormalizeDataScope(query["dataScope"]);
        string? sezonaNaziv = null;

        if (sezonaId.HasValue)
        {
            var sezona = await _db.Sezone.AsNoTracking()
                .Where(s => s.Id == sezonaId.Value)
                .Select(s => new { s.Naziv, s.DatumOd, s.DatumDo })
                .FirstOrDefaultAsync(ct);

            if (sezona is not null)
            {
                fromUtc = DateTime.SpecifyKind(sezona.DatumOd.Date, DateTimeKind.Utc);
                toUtc = DateTime.SpecifyKind(sezona.DatumDo.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                sezonaNaziv = sezona.Naziv;
            }
        }

        if (!fromUtc.HasValue && !toUtc.HasValue)
        {
            var todayUtc = DateTime.UtcNow.Date;
            fromUtc = todayUtc.AddDays(-89);
            toUtc = todayUtc.AddDays(1).AddTicks(-1);
        }

        return new AnalyticsFilters
        {
            SezonaId = sezonaId,
            RequestedFromUtc = requestedFromUtc,
            RequestedToUtc = requestedToUtc,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            StoreId = storeId,
            SupplierId = supplierId,
            SezonaNaziv = sezonaNaziv,
            DataScope = dataScope
        };
    }

    private static (DateTime? previousFromUtc, DateTime? previousToUtc) BuildComparablePreviousRange(
        DateTime? currentFromUtc,
        DateTime? currentToUtc)
    {
        if (!currentFromUtc.HasValue || !currentToUtc.HasValue || currentFromUtc.Value > currentToUtc.Value)
        {
            return (null, null);
        }

        var inclusiveDurationTicks = currentToUtc.Value.Ticks - currentFromUtc.Value.Ticks + 1;
        if (inclusiveDurationTicks <= 0)
        {
            return (null, null);
        }

        var previousToUtc = new DateTime(currentFromUtc.Value.Ticks - 1, DateTimeKind.Utc);
        var previousFromUtc = new DateTime(previousToUtc.Ticks - inclusiveDurationTicks + 1, DateTimeKind.Utc);
        return (previousFromUtc, previousToUtc);
    }

    private static AnalyticsDetailResponseDto BuildShoeTypeDetailProjection(
        AnalyticsDetailResponseDto aggregate,
        List<SalesRow> rows,
        AnalyticsContext context,
        ComparisonMetrics? comparison,
        bool isUnknown)
    {
        var totalRevenue = rows.Sum(x => x.Prihod);
        var totalUnits = rows.Sum(x => x.Kolicina);
        var marginSnapshot = BuildMarginSnapshot(rows, context, totalRevenue);
        var splitSnapshot = BuildSplitSnapshot(rows, context);

        var knownMarginEvidence = context.SalesRows
            .GroupBy(x => x.TipObuceId)
            .Where(group => !string.Equals(group.First().TipObuceNaziv, "Nepoznato", StringComparison.OrdinalIgnoreCase))
            .Select(group =>
            {
                var revenue = group.Sum(x => x.Prihod);
                var margin = BuildMarginSnapshot(group.ToList(), context, revenue);
                return (RevenueWithCost: margin.RevenueWithCost, MarginContribution: margin.MarginContribution);
            })
            .ToList();
        var averageMarginPct = AnalyticsMarginPolicy.ResolveWeightedMarginPct(knownMarginEvidence);
        var unknownRevenue = context.SalesRows
            .Where(x => string.Equals(x.TipObuceNaziv, "Nepoznato", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Prihod);
        var unknownSharePct = totalRevenue > 0m
            ? Math.Round((double)(unknownRevenue / context.SalesRows.Sum(x => x.Prihod) * 100m), 2)
            : 0d;
        var sharePct = context.SalesRows.Sum(x => x.Prihod) > 0m
            ? Math.Round((double)(totalRevenue / context.SalesRows.Sum(x => x.Prihod) * 100m), 2)
            : 0d;
        var hasPreviousPeriodWindow = comparison?.PreviousPeriodRevenue is not null;
        var isNewEntity = hasPreviousPeriodWindow
            && comparison!.PreviousPeriodRevenue <= 0m
            && totalRevenue > 0m;
        var recommendation = AnalyticsDecisionRecommendationEngine.Evaluate(
            new AnalyticsDecisionRecommendationEngine.RecommendationInput(
                IsUnknownEntity: isUnknown,
                TotalRevenue: totalRevenue,
                TotalUnits: totalUnits,
                ItemCount: rows.Select(x => x.ArtikalId).Distinct().Count(),
                SharePct: sharePct,
                MarginPct: marginSnapshot.RevenueWithCost > 0m ? marginSnapshot.MarginPct : 0d,
                MarginCoveragePct: marginSnapshot.MarginDataCoveragePct,
                SplitCoveragePct: splitSnapshot.ComparableRevenueCoveragePct,
                PopRevenueChangePct: comparison?.PopRevenueChangePct,
                PopUnitsChangePct: comparison?.PopUnitsChangePct,
                PreviousPeriodRevenue: comparison?.PreviousPeriodRevenue,
                PreviousPeriodUnits: comparison?.PreviousPeriodUnits,
                HasPreviousPeriodWindow: hasPreviousPeriodWindow,
                IsNewEntity: isNewEntity,
                UnknownBucketSharePct: unknownSharePct),
            averageMarginPct);
        var recommendationAllowed = recommendation.RecommendationAllowed && splitSnapshot.HasComparableSignal;
        var marginQuality = MarginQualityClassifier.ClassifyFromSnapshot(marginSnapshot, totalRevenue);
        var localizedFields = aggregate.Fields
            .Select(field => LocalizeShoeTypeField(field, marginSnapshot, context))
            .ToList();

        return new AnalyticsDetailResponseDto
        {
            Table = aggregate.Table,
            RecordId = aggregate.RecordId,
            Title = aggregate.Title,
            Subtitle = aggregate.Subtitle,
            Fields = localizedFields,
            Metadata = BuildShoeTypeMetadata(context, marginQuality, recommendationAllowed),
            Recommendation = new AnalyticsDetailRecommendationDto
            {
                Status = recommendation.Status,
                Label = ToSerbianRecommendationLabel(recommendation.Status),
                Summary = ToSerbianRecommendationSummary(recommendation.Status, recommendation.ReasonCodes, recommendation.ReliabilityPct),
                ConfidencePct = recommendationAllowed ? recommendation.ConfidencePct : null,
                ReliabilityPct = recommendationAllowed ? recommendation.ReliabilityPct : null,
                DataQualityStatus = recommendation.DataQualityStatus,
                RecommendationAllowed = recommendationAllowed,
                ReasonCodes = recommendation.ReasonCodes
            },
            Provenance = new AnalyticsDetailProvenanceDto
            {
                RequestedFromUtc = context.Filters.RequestedFromUtc,
                RequestedToUtc = context.Filters.RequestedToUtc,
                EffectiveFromUtc = context.Filters.FromUtc,
                EffectiveToUtc = context.Filters.ToUtc,
                Season = context.Filters.SezonaNaziv ?? context.Filters.SezonaId?.ToString(CultureInfo.InvariantCulture),
                StoreId = context.Filters.StoreId,
                DataScope = context.Filters.DataScope,
                GeneratedAtUtc = DateTime.UtcNow,
                Freshness = "fresh",
                DataQualityStatus = recommendation.DataQualityStatus,
                SnapshotActive = context.IsSnapshotActive,
                SnapshotGeneratedAtUtc = context.SnapshotGeneratedAtUtc,
                FallbackApplied = marginSnapshot.SnapshotCostRevenue > 0m || marginSnapshot.EstimatedCostRevenue > 0m,
                RecommendationAllowed = recommendationAllowed
            }
        };
    }

    private static AnalyticsDetailResponseDto BuildColorDetailProjection(
        AnalyticsDetailResponseDto aggregate,
        List<SalesRow> rows,
        AnalyticsContext context,
        ComparisonMetrics? comparison,
        string colorIdentityKey)
    {
        var totalRevenue = rows.Sum(x => x.Prihod);
        var totalUnits = rows.Sum(x => x.Kolicina);
        var marginSnapshot = BuildMarginSnapshot(rows, context, totalRevenue, includeSnapshotCost: false);
        var splitSnapshot = BuildSplitSnapshot(rows, context);
        var totalDatasetRevenue = context.SalesRows.Sum(x => x.Prihod);

        var knownMarginEvidence = context.SalesRows
            .GroupBy(x => ColorIdentityPolicy.Key(x.Boja), StringComparer.Ordinal)
            .Where(group => !ColorIdentityPolicy.IsUnknown(group.Key))
            .Select(group =>
            {
                var revenue = group.Sum(x => x.Prihod);
                var margin = BuildMarginSnapshot(group.ToList(), context, revenue, includeSnapshotCost: false);
                return (RevenueWithCost: margin.RevenueWithCost, MarginContribution: margin.MarginContribution);
            })
            .ToList();
        var averageMarginPct = ColorSignedEvidencePolicy.ResolveWeightedMarginPct(knownMarginEvidence);
        var unknownRevenue = context.SalesRows
            .Where(x => ColorIdentityPolicy.IsUnknown(x.Boja))
            .Sum(x => x.Prihod);
        var unknownSharePct = ColorSignedEvidencePolicy.ResolveNonNegativePercentage(unknownRevenue, totalDatasetRevenue);
        var sharePct = ColorSignedEvidencePolicy.ResolveNonNegativePercentage(totalRevenue, totalDatasetRevenue);
        var hasPreviousPeriodWindow = comparison?.PreviousPeriodRevenue is not null;
        var isNewColor = hasPreviousPeriodWindow
            && comparison!.PreviousPeriodRevenue <= 0m
            && totalRevenue > 0m;
        var recommendation = AnalyticsDecisionRecommendationEngine.Evaluate(
            new AnalyticsDecisionRecommendationEngine.RecommendationInput(
                IsUnknownEntity: ColorIdentityPolicy.IsUnknown(colorIdentityKey),
                TotalRevenue: totalRevenue,
                TotalUnits: totalUnits,
                ItemCount: rows.Select(x => x.ArtikalId).Distinct().Count(),
                SharePct: sharePct ?? 0d,
                MarginPct: marginSnapshot.RevenueWithCost > 0m ? marginSnapshot.MarginPct : 0d,
                MarginCoveragePct: marginSnapshot.MarginDataCoveragePct,
                SplitCoveragePct: splitSnapshot.ComparableRevenueCoveragePct,
                PopRevenueChangePct: comparison?.PopRevenueChangePct,
                PopUnitsChangePct: comparison?.PopUnitsChangePct,
                PreviousPeriodRevenue: comparison?.PreviousPeriodRevenue,
                PreviousPeriodUnits: comparison?.PreviousPeriodUnits,
                HasPreviousPeriodWindow: hasPreviousPeriodWindow,
                IsNewEntity: isNewColor,
                UnknownBucketSharePct: unknownSharePct),
            averageMarginPct);
        var hasComparableNivelacijaSignal = splitSnapshot.RevenueImpactPct.HasValue && splitSnapshot.UnitsImpactPct.HasValue;
        var hasMeasurableEvidence = ColorSignedEvidencePolicy.HasMeasurableRecommendationEvidence(
            totalRevenue,
            marginSnapshot.RevenueWithCost > 0m ? marginSnapshot.MarginPct : null,
            marginSnapshot.MarginDataCoveragePct,
            unknownSharePct);
        var recommendationAllowed = recommendation.RecommendationAllowed
            && hasComparableNivelacijaSignal
            && hasMeasurableEvidence;
        var exposedRecommendationBlocked = !hasMeasurableEvidence;
        var exposedRecommendationStatus = exposedRecommendationBlocked ? "insufficient_data" : recommendation.Status;
        var exposedRecommendationLabel = exposedRecommendationBlocked ? "Insufficient data" : recommendation.Label;
        var exposedRecommendationSummary = exposedRecommendationBlocked
            ? "Signed promet nema pozitivan ili potpun imenilac za pouzdanu preporuku."
            : recommendation.Summary;
        var exposedRecommendationDataQualityStatus = exposedRecommendationBlocked
            ? "insufficient_data"
            : recommendation.DataQualityStatus;
        var exposedReasonCodes = exposedRecommendationBlocked
            ? recommendation.ReasonCodes.Append("signed_denominator_unavailable").Distinct(StringComparer.Ordinal).ToArray()
            : recommendation.ReasonCodes;

        return new AnalyticsDetailResponseDto
        {
            Table = aggregate.Table,
            RecordId = aggregate.RecordId,
            Title = aggregate.Title,
            Subtitle = aggregate.Subtitle,
            Fields = aggregate.Fields,
            Metadata = BuildColorMetadata(
                context,
                marginSnapshot,
                splitSnapshot,
                totalRevenue,
                recommendationAllowed),
            Recommendation = new AnalyticsDetailRecommendationDto
            {
                Status = exposedRecommendationStatus,
                Label = exposedRecommendationLabel,
                Summary = exposedRecommendationSummary,
                ConfidencePct = recommendationAllowed ? recommendation.ConfidencePct : null,
                ReliabilityPct = recommendationAllowed ? recommendation.ReliabilityPct : null,
                DataQualityStatus = exposedRecommendationDataQualityStatus,
                RecommendationAllowed = recommendationAllowed,
                ReasonCodes = exposedReasonCodes
            },
            Provenance = new AnalyticsDetailProvenanceDto
            {
                RequestedFromUtc = context.Filters.RequestedFromUtc,
                RequestedToUtc = context.Filters.RequestedToUtc,
                EffectiveFromUtc = context.Filters.FromUtc,
                EffectiveToUtc = context.Filters.ToUtc,
                Season = context.Filters.SezonaNaziv ?? context.Filters.SezonaId?.ToString(CultureInfo.InvariantCulture),
                StoreId = context.Filters.StoreId,
                DataScope = context.Filters.DataScope,
                GeneratedAtUtc = DateTime.UtcNow,
                Freshness = "fresh",
                DataQualityStatus = exposedRecommendationDataQualityStatus,
                SnapshotActive = false,
                SnapshotGeneratedAtUtc = null,
                FallbackApplied = marginSnapshot.EstimatedCostRevenue > 0m,
                RecommendationAllowed = recommendationAllowed,
                SourceFamily = ColorSalesProvenance.SourceFamily,
                SourceLabel = ColorSalesProvenance.SourceLabel,
                SourceTables = ColorSalesProvenance.SourceTables,
                ObservedPopulation = ColorSalesProvenance.ObservedPopulation,
                CostPolicy = ColorSalesProvenance.CostPolicy,
                PrePostPolicy = ColorSalesProvenance.PrePostPolicy
            }
        };
    }

    private static IReadOnlyList<AnalyticsDetailFieldDto> BuildColorMetadata(
        AnalyticsContext context,
        MarginSnapshot marginSnapshot,
        NivelacijaSplitSnapshot splitSnapshot,
        decimal totalRevenue,
        bool recommendationAllowed)
    {
        var metadata = BuildFilterMetadata(context.Filters).ToList();
        var noCostRevenue = totalRevenue - marginSnapshot.RevenueWithCost;
        metadata.AddRange(
        [
            Field("sourceFamily", "Izvorna porodica", ColorSalesProvenance.SourceFamily, "text"),
            Field("sourceLabel", "Izvor podataka", ColorSalesProvenance.SourceLabel, "text"),
            Field("sourceTables", "Izvorne tabele", ColorSalesProvenance.SourceTables, "text"),
            Field("observedPopulation", "Posmatrana populacija", ColorSalesProvenance.ObservedPopulation, "text"),
            Field("costPolicy", "Politika troška", ColorSalesProvenance.CostPolicy, "text"),
            Field("prePostPolicy", "Politika pre/post kohorte", ColorSalesProvenance.PrePostPolicy, "text"),
            Field("unknownPolicy", "Politika nepoznate boje", ColorSalesProvenance.UnknownPolicy, "text"),
            Field("historicalCostRevenue", "Promet sa istorijskim troškom", marginSnapshot.HistoricalCostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("fallbackCostRevenue", "Promet sa fallback troškom", marginSnapshot.EstimatedCostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("noCostRevenue", "Nepokriveni promet bez troška", noCostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("prePostComparableArticleCount", "Artikli u uporedivoj kohorti", splitSnapshot.ComparableArticleCount.ToString(CultureInfo.InvariantCulture), "number"),
            Field("recommendationAllowed", "Preporuka dozvoljena", recommendationAllowed ? "Da" : "Ne", "text")
        ]);
        return metadata;
    }

    private static MarginSnapshot BuildMarginSnapshot(
        List<SalesRow> rows,
        AnalyticsContext context,
        decimal totalRevenue,
        bool includeSnapshotCost = true)
    {
        var margin = new MarginAccumulator();
        foreach (var row in rows)
        {
            decimal? snapshotCost = null;
            if (includeSnapshotCost
                && row.SaleLineCost is null
                && context.ArticleSnapshotCosts.TryGetValue(row.ArtikalId, out var resolvedSnapshotCost))
            {
                snapshotCost = resolvedSnapshotCost;
            }

            margin.Add(row.Prihod, row.Kolicina, row.SaleLineCost, snapshotCost, row.ProductCostRsd, row.ProductCostLegacy);
        }

        return margin.Build(totalRevenue);
    }

    private static NivelacijaSplitSnapshot BuildSplitSnapshot(List<SalesRow> rows, AnalyticsContext context)
        => AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            context.PrvaNivelacijaPoArtiklu,
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);

    private static AnalyticsDetailFieldDto LocalizeShoeTypeField(
        AnalyticsDetailFieldDto field,
        MarginSnapshot marginSnapshot,
        AnalyticsContext context)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ukupanPromet"] = "Ukupan promet",
            ["ukupnaKolicina"] = "Ukupna količina",
            ["previousPeriodRevenue"] = "Promet prethodnog perioda",
            ["previousPeriodUnits"] = "Količina prethodnog perioda",
            ["popRevenueChangePct"] = "PoP promena prometa (%)",
            ["popUnitsChangePct"] = "PoP promena količine (%)",
            ["preNivelacijePromet"] = "Promet pre nivelacije",
            ["preNivelacijeKolicina"] = "Količina pre nivelacije",
            ["posleNivelacijePromet"] = "Promet posle nivelacije",
            ["posleNivelacijeKolicina"] = "Količina posle nivelacije",
            ["prePostNivelacijaRevenueCoveragePct"] = "Uporedivo pokriće prometa pre/posle (%)",
            ["prePostNivelacijaRevenueImpactPct"] = "Uticaj nivelacije na promet (%)",
            ["prePostNivelacijaUnitsImpactPct"] = "Uticaj nivelacije na količinu (%)",
            ["prePostComparableArticleCount"] = "Artikli sa uporedivim signalom pre/posle",
            ["marginContribution"] = "Maržni doprinos",
            ["marginPct"] = "Marža (%)",
            ["marginDataCoveragePct"] = "Pokriće troška (%)",
            ["fallbackCostCoveragePct"] = "Promet procenjen iz troška artikla (%)",
            ["snapshotCostRevenue"] = "Promet pokriven snimljenim troškom",
            ["snapshotCostCoveragePct"] = "Pokriće snimljenim troškom (%)",
            ["revenueWithCost"] = "Promet sa pokrićem troška",
            ["estimatedCostRevenue"] = "Promet procenjen iz troška artikla",
            ["brojArtikalaSaNivelacijom"] = "Artikli sa nivelacijom",
            ["brojArtikalaUkupno"] = "Ukupan broj artikala",
            ["prePostSignalNote"] = "Napomena za pre/post signal",
            ["marginEstimationNote"] = "Napomena o proceni marže"
        };
        var value = field.Value;
        if (field.Key == "marginPct" && marginSnapshot.RevenueWithCost <= 0m)
        {
            value = null;
        }
        else if (field.Key == "marginContribution" && marginSnapshot.RevenueWithCost <= 0m)
        {
            value = null;
        }
        else if (field.Key == "marginEstimationNote")
        {
            var fallbackShare = marginSnapshot.FallbackCostCoveragePct?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
            var snapshotShare = marginSnapshot.SnapshotCostCoveragePct?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
            value = context.IsSnapshotActive && marginSnapshot.SnapshotCostRevenue > 0m
                ? $"Istorijska nabavna cena nije sačuvana za deo prometa; korišćen je snimljeni trošak ({snapshotShare}%) i trošak artikla ({fallbackShare}%)."
                : $"Istorijska nabavna cena nije sačuvana za {fallbackShare}% prometa, pa je taj deo marže procenjen iz troška artikla.";
        }

        return new AnalyticsDetailFieldDto
        {
            Key = field.Key,
            Label = labels.TryGetValue(field.Key, out var label) ? label : field.Label,
            Value = value,
            DataType = field.DataType,
            Highlight = field.Highlight
        };
    }

    private static IReadOnlyList<AnalyticsDetailFieldDto> BuildShoeTypeMetadata(
        AnalyticsContext context,
        MarginQualityClassifier.MarginQualityResult marginQuality,
        bool recommendationAllowed)
    {
        var filters = context.Filters;
        return
        [
            Field("requestedFromDate", "Traženi period od", filters.RequestedFromUtc?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "date"),
            Field("requestedToDate", "Traženi period do", filters.RequestedToUtc?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "date"),
            Field("effectiveFromDate", "Efektivni period od", filters.FromUtc?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "date"),
            Field("effectiveToDate", "Efektivni period do", filters.ToUtc?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "date"),
            Field("sezona", "Sezona", filters.SezonaNaziv ?? filters.SezonaId?.ToString(CultureInfo.InvariantCulture), "text"),
            Field("storeId", "Objekat", filters.StoreId?.ToString(CultureInfo.InvariantCulture), "number"),
            Field("dataScope", "Opseg podataka", ToSerbianDataScope(filters.DataScope), "text"),
            Field("generatedAt", "Generisano", DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm:ss 'UTC'", CultureInfo.InvariantCulture), "datetime"),
            Field("freshness", "Svežina podataka", "Sveže", "text"),
            Field("marginQuality", "Kvalitet marže", marginQuality.Label, "text"),
            Field("snapshotStatus", "Snimljeni trošak", context.IsSnapshotActive ? "Aktivan" : "Nije aktivan", "text"),
            Field("snapshotGeneratedAt", "Vreme snimka troška", context.SnapshotGeneratedAtUtc?.ToString("dd.MM.yyyy HH:mm:ss 'UTC'", CultureInfo.InvariantCulture), "datetime"),
            Field("recommendationAllowed", "Preporuka dozvoljena", recommendationAllowed ? "Da" : "Ne", "text")
        ];
    }

    private static bool IsUnknownShoeTypeId(string? id)
    {
        if (string.Equals(id, "unknown-nepoznato", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!(id ?? string.Empty).StartsWith("unknown-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = Uri.UnescapeDataString(id!["unknown-".Length..]);
        return string.Equals(suffix.Trim(), "Nepoznato", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToSerbianDataScope(string scope)
        => scope.ToLowerInvariant() switch
        {
            "imported" => "Uvezeni podaci",
            "existing" => "Postojeći podaci",
            _ => "Svi podaci"
        };

    private static string ToSerbianRecommendationLabel(string status)
        => status switch
        {
            "increase_focus" => "Povećati fokus",
            "maintain" => "Održati",
            "review" => "Proveriti",
            "do_not_trust" => "Ne verovati preporuci",
            _ => "Nedovoljno podataka"
        };

    private static string ToSerbianRecommendationSummary(
        string status,
        IReadOnlyCollection<string> reasonCodes,
        double reliabilityPct)
    {
        if (reasonCodes.Contains("unknown_entity"))
        {
            return "Identitet tipa obuće nije poznat; preporuka nije bezbedna za poslovnu odluku.";
        }

        if (reasonCodes.Contains("missing_known_margin_baseline"))
        {
            return "Nedostaje uporediva osnova poznate marže; nema dovoljno dokaza za pouzdanu preporuku.";
        }

        if (reasonCodes.Contains("missing_split_coverage"))
        {
            return "Nedostaje uporediv signal pre i posle nivelacije; preporuka nije potvrđena.";
        }

        if (reasonCodes.Contains("tiny_sample"))
        {
            return "Uzorak je premali po prometu, količini ili broju artikala za pouzdanu preporuku.";
        }

        return status switch
        {
            "increase_focus" => $"Trend i marža podržavaju povećanje fokusa uz pouzdanost od {reliabilityPct:0.#}%.",
            "maintain" => $"Signal je stabilan bez snažnog rasta ili pada; pouzdanost je {reliabilityPct:0.#}%.",
            "review" => "Signali su pomešani; proveriti podatke pre promene fokusa nabavke.",
            "do_not_trust" => "Kvalitet podataka ili signal marže je prenizak za automatsku odluku.",
            _ => "Nema dovoljno dokaza za automatsku preporuku."
        };
    }

    private static AnalyticsDetailResponseDto? BuildAggregatedDetail(
        string table,
        string recordId,
        string title,
        string subtitle,
        List<SalesRow> rows,
        AnalyticsContext context,
        ComparisonMetrics? comparison = null,
        bool includeSnapshotCost = true)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        decimal totalRevenue = 0m;
        int totalQty = 0;
        var margin = new MarginAccumulator();

        var articleIds = new HashSet<int>();

        foreach (var row in rows)
        {
            totalRevenue += row.Prihod;
            totalQty += row.Kolicina;
            articleIds.Add(row.ArtikalId);
            decimal? snapshotCost = null;
            if (includeSnapshotCost
                && row.SaleLineCost is null
                && context.ArticleSnapshotCosts.TryGetValue(row.ArtikalId, out var sc))
                snapshotCost = sc;
            margin.Add(
                row.Prihod,
                row.Kolicina,
                row.SaleLineCost,
                snapshotCost,
                row.ProductCostRsd,
                row.ProductCostLegacy);
        }

        var marginSnapshot = margin.Build(totalRevenue);
        var splitSnapshot = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            context.PrvaNivelacijaPoArtiklu,
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);
        var estimatedMargin = marginSnapshot.EstimatedCostRevenue > 0m || marginSnapshot.SnapshotCostRevenue > 0m;

        var popRevenueLabel = comparison?.PopRevenueChangePct?.ToString("0.00", CultureInfo.InvariantCulture)
            ?? (comparison?.PreviousPeriodRevenue.HasValue == true && comparison.PreviousPeriodRevenue.Value <= 0m && totalRevenue > 0m
                ? "Novo"
                : null);
        var popUnitsLabel = comparison?.PopUnitsChangePct?.ToString("0.00", CultureInfo.InvariantCulture)
            ?? (comparison?.PreviousPeriodUnits.HasValue == true && comparison.PreviousPeriodUnits.Value <= 0 && totalQty > 0
                ? "Novo"
                : null);
        var fields = new List<AnalyticsDetailFieldDto>
        {
            Field("ukupanPromet", "Ukupan promet", Math.Round(totalRevenue, 2).ToString("0.00", CultureInfo.InvariantCulture), "currency", true),
            Field("ukupnaKolicina", "Ukupna kolicina", totalQty.ToString(CultureInfo.InvariantCulture), "number")
        };

        if (comparison is not null)
        {
            fields.Add(Field("previousPeriodRevenue", "Prethodni period promet", comparison.PreviousPeriodRevenue?.ToString("0.00", CultureInfo.InvariantCulture), "currency"));
            fields.Add(Field("previousPeriodUnits", "Prethodni period kolicina", comparison.PreviousPeriodUnits?.ToString(CultureInfo.InvariantCulture), "number"));
            fields.Add(Field("popRevenueChangePct", "PoP trend prometa %", popRevenueLabel, "percent", comparison.PopRevenueChangePct.HasValue));
            fields.Add(Field("popUnitsChangePct", "PoP trend kolicine %", popUnitsLabel, "percent", comparison.PopUnitsChangePct.HasValue));
        }

        fields.AddRange(
        [
            Field("preNivelacijePromet", "Pre nivelacije promet", splitSnapshot.PreRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("preNivelacijeKolicina", "Pre nivelacije kolicina", splitSnapshot.PreQuantity.ToString(CultureInfo.InvariantCulture), "number"),
            Field("posleNivelacijePromet", "Posle nivelacije promet", splitSnapshot.PostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("posleNivelacijeKolicina", "Posle nivelacije kolicina", splitSnapshot.PostQuantity.ToString(CultureInfo.InvariantCulture), "number"),
            Field("prePostNivelacijaRevenueCoveragePct", "Pre/post uporedivo pokrice prometa %", splitSnapshot.ComparableRevenueCoveragePct?.ToString("0.00", CultureInfo.InvariantCulture), "percent"),
            Field("prePostNivelacijaRevenueImpactPct", "Pre/post nivelacija impact %", splitSnapshot.RevenueImpactPct?.ToString("0.00", CultureInfo.InvariantCulture), "percent", splitSnapshot.RevenueImpactPct.HasValue),
            Field("prePostNivelacijaUnitsImpactPct", "Pre/post nivelacija impact kolicine %", splitSnapshot.UnitsImpactPct?.ToString("0.00", CultureInfo.InvariantCulture), "percent"),
            Field("prePostComparableArticleCount", "Artikli sa uporedivim pre/post signalom", splitSnapshot.ComparableArticleCount.ToString(CultureInfo.InvariantCulture), "number"),
            Field("marginContribution", estimatedMargin ? "Procenjeni marzni doprinos" : "Marzni doprinos", marginSnapshot.MarginContribution.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("marginPct", estimatedMargin ? "Procenjena marza %" : "Marza %", marginSnapshot.MarginPct.ToString("0.00", CultureInfo.InvariantCulture), "percent"),
            Field("marginDataCoveragePct", "Pokrice istorijskog troska %", marginSnapshot.HistoricalMarginCoveragePct?.ToString("0.00", CultureInfo.InvariantCulture), "percent"),
            Field("fallbackCostCoveragePct", "Promet procenjen iz fallback troska %", marginSnapshot.FallbackCostCoveragePct?.ToString("0.00", CultureInfo.InvariantCulture), "percent"),
            Field("snapshotCostRevenue", "Promet pokriven snapshot troskom", marginSnapshot.SnapshotCostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("snapshotCostCoveragePct", "Pokrivenost snapshot troskom %", marginSnapshot.SnapshotCostCoveragePct?.ToString("0.00", CultureInfo.InvariantCulture), "percent"),
            Field("revenueWithCost", "Promet sa istorijskim troskom", marginSnapshot.HistoricalCostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("estimatedCostRevenue", "Promet procenjen iz fallback troska", marginSnapshot.EstimatedCostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("brojArtikalaSaNivelacijom", "Artikli sa nivelacijom", splitSnapshot.ArticleCountWithNivelacija.ToString(CultureInfo.InvariantCulture), "number"),
            Field("brojArtikalaUkupno", "Ukupan broj artikala", articleIds.Count.ToString(CultureInfo.InvariantCulture), "number")
        ]);

        if (!string.IsNullOrWhiteSpace(splitSnapshot.SignalNote))
        {
            fields.Add(Field("prePostSignalNote", "Napomena za pre/post signal", splitSnapshot.SignalNote, "text"));
        }

        if (estimatedMargin)
        {
            var fallbackShare = marginSnapshot.FallbackCostCoveragePct?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
            var snapshotShare = marginSnapshot.SnapshotCostCoveragePct?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
            var noteText = context.IsSnapshotActive && marginSnapshot.SnapshotCostRevenue > 0m
                ? $"Istorijska nabavna cena nije sacuvana na prodajnim stavkama za deo prometa; koriscen je snapshot trosak ({snapshotShare}%) i fallback trosak artikla ({fallbackShare}%)."
                : $"Istorijska nabavna cena nije sacuvana na prodajnim stavkama za {fallbackShare}% prometa, pa je marza za taj deo procenjena iz fallback troska artikla.";
            fields.Add(Field(
                "marginEstimationNote",
                "Napomena za marzu",
                noteText,
                "text"));
        }

        return new AnalyticsDetailResponseDto
        {
            Table = table,
            RecordId = recordId,
            Title = title,
            Subtitle = subtitle,
            Fields = fields,
            Metadata = BuildFilterMetadata(context.Filters)
        };
    }

    private static IReadOnlyList<AnalyticsDetailFieldDto> BuildFilterMetadata(AnalyticsFilters filters)
    {
        return
        [
            Field("sezona", "Sezona", filters.SezonaNaziv ?? filters.SezonaId?.ToString(CultureInfo.InvariantCulture), "text"),
            Field("fromDate", "Od", filters.FromUtc?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "date"),
            Field("toDate", "Do", filters.ToUtc?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "date"),
            Field("storeId", "Objekat", filters.StoreId?.ToString(CultureInfo.InvariantCulture), "number"),
            Field("supplierId", "Dobavljac", filters.SupplierId?.ToString(CultureInfo.InvariantCulture), "number"),
            Field("dataScope", "Data scope", filters.DataScope, "text")
        ];
    }

    private static AnalyticsDetailFieldDto Field(string key, string label, string? value, string? dataType = null, bool highlight = false)
        => new()
        {
            Key = key,
            Label = label,
            Value = value,
            DataType = dataType,
            Highlight = highlight
        };

    private static int? TryParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static DateTime? TryParseDateTime(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue) return null;
        var date = value.Value;
        return date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : date.ToUniversalTime();
    }

    private static DateTime? ExpandInclusiveDateEnd(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var date = value.Value;
        return date.TimeOfDay == TimeSpan.Zero
            ? date.Date.AddDays(1).AddTicks(-1)
            : date;
    }

    private static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

}
