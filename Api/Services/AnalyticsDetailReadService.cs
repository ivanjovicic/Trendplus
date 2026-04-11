using Api.Models;
using Application.Analytics;
using Domain.Model;
using Infrastructure.DbContexts;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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

    public AnalyticsDetailReadService(
        TrendplusDbContext db,
        IDnevnikPromenaReadService dnevnikPromenaReadService)
    {
        _db = db;
        _dnevnikPromenaReadService = dnevnikPromenaReadService;
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
        if (!int.TryParse(id, out var shoeTypeId))
        {
            return null;
        }

        var context = await BuildAnalyticsContextAsync(query, ct);
        var rows = context.SalesRows.Where(x => x.TipObuceId == shoeTypeId).ToList();
        var title = rows.FirstOrDefault()?.TipObuceNaziv ?? $"Tip obuce {id}";
        return BuildAggregatedDetail("shoe-type-sales-stats", id, title, "Prodaja po tipu obuce", rows, context);
    }

    private async Task<AnalyticsDetailResponseDto?> GetColorSalesDetailAsync(string id, IQueryCollection query, CancellationToken ct)
    {
        var colorKey = Uri.UnescapeDataString(id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(colorKey))
        {
            return null;
        }

        var normalizedColor = NormalizeColor(colorKey);
        var context = await BuildAnalyticsContextAsync(query, ct);
        var rows = context.SalesRows.Where(x => NormalizeColor(x.Boja) == normalizedColor).ToList();
        return BuildAggregatedDetail("color-sales-stats", id ?? string.Empty, normalizedColor, "Prodaja po boji artikla", rows, context);
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
            Field("boja", "Boja", NormalizeColor(article?.Boja ?? rows[0].Boja), "text")
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
                Boja = NormalizeColor(a.Boja)
            })
            .Where(x => !filters.SupplierId.HasValue || x.DobavljacId == filters.SupplierId.Value)
            .ToListAsync(ct);

        return new AnalyticsContext
        {
            Filters = filters,
            PrvaNivelacijaPoArtiklu = prvaNivelacijaPoArtiklu,
            SalesRows = salesRows
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

    private async Task<AnalyticsFilters> ParseFiltersAsync(IQueryCollection query, CancellationToken ct)
    {
        var sezonaId = TryParseInt(query["sezonaId"]);
        var fromUtc = NormalizeUtc(TryParseDateTime(query["fromDate"]));
        var toUtc = NormalizeUtc(TryParseDateTime(query["toDate"]));
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

    private static AnalyticsDetailResponseDto? BuildAggregatedDetail(
        string table,
        string recordId,
        string title,
        string subtitle,
        List<SalesRow> rows,
        AnalyticsContext context,
        ComparisonMetrics? comparison = null)
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
            margin.Add(
                row.Prihod,
                row.Kolicina,
                row.SaleLineCost,
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
        var estimatedMargin = marginSnapshot.EstimatedCostRevenue > 0m;

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
            Field("marginDataCoveragePct", "Pokrice istorijske marze %", marginSnapshot.HistoricalMarginCoveragePct?.ToString("0.00", CultureInfo.InvariantCulture), "percent"),
            Field("fallbackCostCoveragePct", "Promet procenjen iz master troska %", marginSnapshot.FallbackCostCoveragePct?.ToString("0.00", CultureInfo.InvariantCulture), "percent"),
            Field("revenueWithCost", "Promet sa istorijskom nabavnom cenom", marginSnapshot.HistoricalCostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("estimatedCostRevenue", "Promet procenjen iz master troska", marginSnapshot.EstimatedCostRevenue.ToString("0.00", CultureInfo.InvariantCulture), "currency"),
            Field("brojArtikalaSaNivelacijom", "Artikli sa nivelacijom", splitSnapshot.ArticleCountWithNivelacija.ToString(CultureInfo.InvariantCulture), "number"),
            Field("brojArtikalaUkupno", "Ukupan broj artikala", articleIds.Count.ToString(CultureInfo.InvariantCulture), "number")
        ]);

        if (!string.IsNullOrWhiteSpace(splitSnapshot.SignalNote))
        {
            fields.Add(Field("prePostSignalNote", "Napomena za pre/post signal", splitSnapshot.SignalNote, "text"));
        }

        if (estimatedMargin)
        {
            var estimatedShareText = marginSnapshot.FallbackCostCoveragePct?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
            fields.Add(Field(
                "marginEstimationNote",
                "Napomena za marzu",
                $"Istorijska nabavna cena nije sacuvana na prodajnim stavkama za {estimatedShareText}% prometa, pa je marza za taj deo procenjena iz trenutnog master troska artikla.",
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

    private static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    private static string NormalizeColor(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Nepoznato" : value.Trim();
}
