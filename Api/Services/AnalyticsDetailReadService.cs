using Api.Models;
using Domain.Model;
using Infrastructure.DbContexts;
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
    }

    private sealed class SalesRow
    {
        public int ArtikalId { get; init; }
        public int Kolicina { get; init; }
        public decimal Prihod { get; init; }
        public decimal? NabavnaCena { get; init; }
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
                Field("datum", "Datum", detail.Datum.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), "datetime"),
                Field("artikalId", "Artikal ID", detail.ArtikalId?.ToString(), "number"),
                Field("nazivArtikla", "Naziv artikla", detail.NazivArtikla, "text"),
                Field("kolicina", "Kolicina", detail.Kolicina?.ToString(), "number"),
                Field("staraCena", "Stara cena", detail.StaraCena?.ToString("0.00"), "currency"),
                Field("novaCena", "Nova cena", detail.NovaCena?.ToString("0.00"), "currency", detail.StaraCena != detail.NovaCena),
                Field("iznos", "Iznos", detail.Iznos.ToString("0.00"), "currency", true),
                Field("brojRacuna", "Broj racuna", detail.BrojRacuna, "text"),
                Field("korisnikIme", "Korisnik", detail.KorisnikIme, "text"),
                Field("komentar", "Komentar", detail.Komentar, "text"),
                Field("dataOrigin", "Data origin", detail.DataOrigin, "text"),
                Field("sourceId", "Source ID", detail.SourceId.ToString(), "number")
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
            rows = context.SalesRows.Where(x => !x.DobavljacId.HasValue).ToList();
            title = "Nepoznato";
        }
        else
        {
            return null;
        }

        return BuildAggregatedDetail("supplier-sales-stats", id, title, "Prodaja po dobavljacima", rows, context);
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
            Field("artikalId", "Artikal ID", artikalId.ToString(), "number"),
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
            select new SalesRow
            {
                ArtikalId = a.Id,
                Kolicina = ps.Kolicina,
                Prihod = ps.Kolicina * ps.Cena,
                NabavnaCena = ps.NabavnaCena ?? a.NabavnaCena,
                DatumProdaje = pz.DatumProdaje,
                NazivArtikla = a.Naziv ?? $"Artikal {a.Id}",
                SifraArtikla = a.PLU ?? a.Id.ToString(),
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

    private async Task<AnalyticsFilters> ParseFiltersAsync(IQueryCollection query, CancellationToken ct)
    {
        var sezonaId = TryParseInt(query["sezonaId"]);
        var fromUtc = NormalizeUtc(TryParseDateTime(query["fromDate"]));
        var toUtc = NormalizeUtc(TryParseDateTime(query["toDate"]));
        var storeId = TryParseInt(query["storeId"]);
        var supplierId = TryParseInt(query["supplierId"]);
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
            SezonaNaziv = sezonaNaziv
        };
    }

    private static AnalyticsDetailResponseDto? BuildAggregatedDetail(
        string table,
        string recordId,
        string title,
        string subtitle,
        List<SalesRow> rows,
        AnalyticsContext context)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        decimal preNivRevenue = 0m;
        decimal postNivRevenue = 0m;
        decimal totalRevenue = 0m;
        int preNivQty = 0;
        int postNivQty = 0;
        int totalQty = 0;
        decimal totalCost = 0m;
        decimal revenueWithCost = 0m;

        var articleIds = new HashSet<int>();
        var articleIdsWithNivelacija = new HashSet<int>();

        foreach (var row in rows)
        {
            totalRevenue += row.Prihod;
            totalQty += row.Kolicina;
            articleIds.Add(row.ArtikalId);

            if (row.NabavnaCena.HasValue)
            {
                totalCost += row.Kolicina * row.NabavnaCena.Value;
                revenueWithCost += row.Prihod;
            }

            if (!context.PrvaNivelacijaPoArtiklu.TryGetValue(row.ArtikalId, out var nivDatum))
            {
                continue;
            }

            articleIdsWithNivelacija.Add(row.ArtikalId);
            if (row.DatumProdaje < nivDatum)
            {
                preNivRevenue += row.Prihod;
                preNivQty += row.Kolicina;
            }
            else
            {
                postNivRevenue += row.Prihod;
                postNivQty += row.Kolicina;
            }
        }

        var marginPct = revenueWithCost > 0m
            ? Math.Round((double)((revenueWithCost - totalCost) / revenueWithCost * 100m), 2)
            : 0d;
        var marginContribution = Math.Round(revenueWithCost - totalCost, 2);
        var marginDataCoveragePct = totalRevenue > 0m
            ? Math.Round((double)(revenueWithCost / totalRevenue * 100m), 2)
            : (double?)null;

        var promenaPrometa = preNivRevenue > 0m
            ? Math.Round((double)((postNivRevenue - preNivRevenue) / preNivRevenue * 100m), 2)
            : (double?)null;

        var promenaKolicine = preNivQty > 0
            ? Math.Round((postNivQty - preNivQty) / (double)preNivQty * 100d, 2)
            : (double?)null;

        return new AnalyticsDetailResponseDto
        {
            Table = table,
            RecordId = recordId,
            Title = title,
            Subtitle = subtitle,
            Fields =
            [
                Field("ukupanPromet", "Ukupan promet", Math.Round(totalRevenue, 2).ToString("0.00"), "currency", true),
                Field("ukupnaKolicina", "Ukupna kolicina", totalQty.ToString(), "number"),
                Field("preNivelacijePromet", "Pre nivelacije promet", Math.Round(preNivRevenue, 2).ToString("0.00"), "currency"),
                Field("preNivelacijeKolicina", "Pre nivelacije kolicina", preNivQty.ToString(), "number"),
                Field("posleNivelacijePromet", "Posle nivelacije promet", Math.Round(postNivRevenue, 2).ToString("0.00"), "currency"),
                Field("posleNivelacijeKolicina", "Posle nivelacije kolicina", postNivQty.ToString(), "number"),
                Field("promenaPrometa", "Promena prometa %", promenaPrometa?.ToString("0.00"), "percent", promenaPrometa.HasValue),
                Field("promenaKolicine", "Promena kolicine %", promenaKolicine?.ToString("0.00"), "percent"),
                Field("marginContribution", "Marzni doprinos", marginContribution.ToString("0.00"), "currency"),
                Field("marginPct", "Marza %", marginPct.ToString("0.00"), "percent"),
                Field("marginDataCoveragePct", "Pokrice marze %", marginDataCoveragePct?.ToString("0.00"), "percent"),
                Field("revenueWithCost", "Promet sa poznatom nabavnom cenom", Math.Round(revenueWithCost, 2).ToString("0.00"), "currency"),
                Field("brojArtikalaSaNivelacijom", "Artikli sa nivelacijom", articleIdsWithNivelacija.Count.ToString(), "number"),
                Field("brojArtikalaUkupno", "Ukupan broj artikala", articleIds.Count.ToString(), "number")
            ],
            Metadata = BuildFilterMetadata(context.Filters)
        };
    }

    private static IReadOnlyList<AnalyticsDetailFieldDto> BuildFilterMetadata(AnalyticsFilters filters)
    {
        return
        [
            Field("sezona", "Sezona", filters.SezonaNaziv ?? filters.SezonaId?.ToString(), "text"),
            Field("fromDate", "Od", filters.FromUtc?.ToString("dd.MM.yyyy"), "date"),
            Field("toDate", "Do", filters.ToUtc?.ToString("dd.MM.yyyy"), "date"),
            Field("storeId", "Objekat", filters.StoreId?.ToString(), "number"),
            Field("supplierId", "Dobavljac", filters.SupplierId?.ToString(), "number")
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

    private static string NormalizeColor(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Nepoznato" : value.Trim();
}
