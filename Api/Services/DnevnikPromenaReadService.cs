using Api.Models;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IDnevnikPromenaReadService
{
    Task<DnevnikPromenaListResponseDto> GetPagedAsync(DnevnikPromenaListQuery query, CancellationToken ct = default);
    Task<DnevnikPromenaDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
}

public sealed class DnevnikPromenaReadService : IDnevnikPromenaReadService
{
    private sealed class TrendMovementProjection
    {
        public int Id { get; init; }
        public string TipPromene { get; init; } = string.Empty;
        public DateTime Datum { get; init; }
        public decimal Iznos { get; init; }
        public string? BrojRacuna { get; init; }
        public int? ArtikalId { get; init; }
        public string? ArtikalNaziv { get; init; }
        public int? DobavljacId { get; init; }
        public string? DobavljacNaziv { get; init; }
        public decimal? StaraProdajnaCena { get; init; }
        public decimal? NovaProdajnaCena { get; init; }
        public int? Kolicina { get; init; }
        public string? Komentar { get; init; }
        public string? KorisnikIme { get; init; }
        public string? DataOrigin { get; init; }
    }

    private readonly TrendplusDbContext _trendDb;
    private readonly AnalyticsDbContext _analyticsDb;
    private readonly ILogger<DnevnikPromenaReadService> _logger;

    public DnevnikPromenaReadService(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        ILogger<DnevnikPromenaReadService> logger)
    {
        _trendDb = trendDb;
        _analyticsDb = analyticsDb;
        _logger = logger;
    }

    public async Task<DnevnikPromenaListResponseDto> GetPagedAsync(DnevnikPromenaListQuery query, CancellationToken ct = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 50 : Math.Min(query.PageSize, 200);
        var normalizedSortBy = (query.SortBy ?? "datum").Trim().ToLowerInvariant();
        var normalizedSortDir = string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

        var dataQuery = BuildTrendMovementQuery(query.DataScope);

        if (!string.IsNullOrWhiteSpace(query.TipPromene))
            dataQuery = dataQuery.Where(x => x.TipPromene == query.TipPromene);

        if (query.ArtikalId.HasValue)
            dataQuery = dataQuery.Where(x => x.ArtikalId == query.ArtikalId.Value);

        if (!string.IsNullOrWhiteSpace(query.Naziv))
            dataQuery = dataQuery.Where(x => x.ArtikalNaziv != null && x.ArtikalNaziv.Contains(query.Naziv));

        if (!string.IsNullOrWhiteSpace(query.BrojRacuna))
            dataQuery = dataQuery.Where(x => x.BrojRacuna != null && x.BrojRacuna.Contains(query.BrojRacuna));

        if (query.FromDate.HasValue)
            dataQuery = dataQuery.Where(x => x.Datum >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            dataQuery = dataQuery.Where(x => x.Datum <= query.ToDate.Value);

        dataQuery = normalizedSortBy switch
        {
            "tippromene" => normalizedSortDir == "asc" ? dataQuery.OrderBy(x => x.TipPromene) : dataQuery.OrderByDescending(x => x.TipPromene),
            "iznos" => normalizedSortDir == "asc" ? dataQuery.OrderBy(x => x.Iznos) : dataQuery.OrderByDescending(x => x.Iznos),
            "naziv" => normalizedSortDir == "asc" ? dataQuery.OrderBy(x => x.ArtikalNaziv) : dataQuery.OrderByDescending(x => x.ArtikalNaziv),
            _ => normalizedSortDir == "asc" ? dataQuery.OrderBy(x => x.Datum) : dataQuery.OrderByDescending(x => x.Datum)
        };

        var total = await dataQuery.CountAsync(ct);
        var items = await dataQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToListItemDto(x))
            .ToListAsync(ct);

        return new DnevnikPromenaListResponseDto
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortBy = normalizedSortBy,
            SortDir = normalizedSortDir
        };
    }

    public async Task<DnevnikPromenaDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var primary = await BuildTrendMovementQuery("all")
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync(ct);

        if (primary is not null)
        {
            return ToDetailDto(primary, primary.Id);
        }

        var fallback = await _analyticsDb.InventoryMovementFacts
            .AsNoTracking()
            .Where(x => x.SourceId == id)
            .OrderByDescending(x => x.DataOrigin == "access")
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.SourceId,
                x.TipPromene,
                x.Datum,
                x.ArtikalId,
                x.Kolicina,
                x.StaraProdajnaCena,
                x.NovaProdajnaCena,
                x.Iznos,
                x.BrojDokumenta,
                x.KorisnikIme,
                x.DataOrigin
            })
            .FirstOrDefaultAsync(ct);

        if (fallback is null)
        {
            return null;
        }

        var nazivArtikla = fallback.ArtikalId.HasValue
            ? await ResolveProductNameAsync(fallback.ArtikalId.Value, ct)
            : null;

        _logger.LogInformation(
            "DnevnikPromena detail for {Id} resolved from InventoryMovementFacts fallback with data origin {DataOrigin}",
            id,
            fallback.DataOrigin);

        return new DnevnikPromenaDetailDto
        {
            Id = fallback.SourceId,
            TipPromene = fallback.TipPromene,
            Datum = DateTime.SpecifyKind(fallback.Datum, DateTimeKind.Utc),
            ArtikalId = fallback.ArtikalId,
            NazivArtikla = NormalizeText(nazivArtikla),
            Kolicina = fallback.Kolicina,
            StaraCena = fallback.StaraProdajnaCena,
            NovaCena = fallback.NovaProdajnaCena,
            Iznos = fallback.Iznos,
            BrojRacuna = NormalizeText(fallback.BrojDokumenta),
            KorisnikIme = NormalizeText(fallback.KorisnikIme),
            Komentar = null,
            DataOrigin = NormalizeText(fallback.DataOrigin),
            SourceId = fallback.SourceId
        };
    }

    private IQueryable<TrendMovementProjection> BuildTrendMovementQuery(string? dataScope)
    {
        var normalizedDataScope = (dataScope ?? "all").Trim().ToLowerInvariant();

        var dnevnikBaseQuery = _trendDb.DnevnikPromena.AsNoTracking().AsQueryable();
        dnevnikBaseQuery = normalizedDataScope switch
        {
            "imported" => dnevnikBaseQuery.Where(dp => dp.DataOrigin == "access"),
            "existing" => dnevnikBaseQuery.Where(dp => dp.DataOrigin == "existing" || dp.DataOrigin == null || dp.DataOrigin == ""),
            _ => dnevnikBaseQuery
        };

        return from dp in dnevnikBaseQuery
               join a in _trendDb.Artikli.AsNoTracking() on dp.ArtikalId equals a.Id into artikli
               from artikal in artikli.DefaultIfEmpty()
               join d in _trendDb.Dobavljaci.AsNoTracking() on dp.DobavljacId equals d.Id into dobavljaci
               from dobavljac in dobavljaci.DefaultIfEmpty()
               select new TrendMovementProjection
               {
                   Id = dp.Id,
                   TipPromene = dp.TipPromene,
                   Datum = dp.Datum,
                   Iznos = dp.Iznos,
                   BrojRacuna = dp.BrojRacuna,
                   ArtikalId = dp.ArtikalId,
                   ArtikalNaziv = artikal != null ? artikal.Naziv : null,
                   DobavljacId = dp.DobavljacId,
                   DobavljacNaziv = dobavljac != null ? dobavljac.Naziv : null,
                   StaraProdajnaCena = dp.StaraProdajnaCena,
                   NovaProdajnaCena = dp.NovaProdajnaCena,
                   Kolicina = dp.Kolicina,
                   Komentar = dp.Komentar,
                   KorisnikIme = dp.KorisnikIme,
                   DataOrigin = dp.DataOrigin
               };
    }

    private async Task<string?> ResolveProductNameAsync(int artikalId, CancellationToken ct)
    {
        var trendName = await _trendDb.Artikli
            .AsNoTracking()
            .Where(x => x.Id == artikalId)
            .Select(x => x.Naziv)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(trendName))
        {
            return trendName;
        }

        return await _analyticsDb.ProductsDim
            .AsNoTracking()
            .Where(x => x.ProductId == artikalId)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.ProductName)
            .FirstOrDefaultAsync(ct);
    }

    private static DnevnikPromenaListItemDto ToListItemDto(TrendMovementProjection source)
        => new()
        {
            Id = source.Id,
            TipPromene = source.TipPromene,
            Datum = DateTime.SpecifyKind(source.Datum, DateTimeKind.Utc),
            Iznos = source.Iznos,
            BrojRacuna = NormalizeText(source.BrojRacuna),
            ArtikalId = source.ArtikalId,
            ArtikalNaziv = NormalizeText(source.ArtikalNaziv),
            DobavljacId = source.DobavljacId,
            DobavljacNaziv = NormalizeText(source.DobavljacNaziv),
            StaraProdajnaCena = source.StaraProdajnaCena,
            NovaProdajnaCena = source.NovaProdajnaCena,
            Komentar = NormalizeText(source.Komentar),
            KorisnikIme = NormalizeText(source.KorisnikIme),
            DataOrigin = NormalizeText(source.DataOrigin)
        };

    private static DnevnikPromenaDetailDto ToDetailDto(TrendMovementProjection source, int sourceId)
        => new()
        {
            Id = source.Id,
            TipPromene = source.TipPromene,
            Datum = DateTime.SpecifyKind(source.Datum, DateTimeKind.Utc),
            ArtikalId = source.ArtikalId,
            NazivArtikla = NormalizeText(source.ArtikalNaziv),
            Kolicina = source.Kolicina,
            StaraCena = source.StaraProdajnaCena,
            NovaCena = source.NovaProdajnaCena,
            Iznos = source.Iznos,
            BrojRacuna = NormalizeText(source.BrojRacuna),
            KorisnikIme = NormalizeText(source.KorisnikIme),
            Komentar = NormalizeText(source.Komentar),
            DataOrigin = NormalizeText(source.DataOrigin),
            SourceId = sourceId
        };

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
