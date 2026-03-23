using Api.Models;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

        try
        {
            var dataQuery = BuildTrendMovementQuery(query.DataScope);
            dataQuery = ApplyFilters(dataQuery, query, includeArtikalFilters: true);
            dataQuery = ApplySorting(dataQuery, normalizedSortBy, normalizedSortDir);

            return await BuildPagedResponseAsync(dataQuery, pageNumber, pageSize, normalizedSortBy, normalizedSortDir, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "DnevnikPromena query hit legacy schema (missing columns). Falling back to compatibility projection.");

            if (query.ArtikalId.HasValue || !string.IsNullOrWhiteSpace(query.Naziv) || !string.Equals(query.DataScope, "all", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Legacy fallback ignored unsupported filters (ArtikalId/Naziv/DataScope) because required columns are missing.");
            }

            var legacyQuery = BuildLegacyTrendMovementQuery();
            legacyQuery = ApplyFilters(legacyQuery, query, includeArtikalFilters: false);
            legacyQuery = ApplySorting(legacyQuery, normalizedSortBy, normalizedSortDir);

            return await BuildPagedResponseAsync(legacyQuery, pageNumber, pageSize, normalizedSortBy, normalizedSortDir, ct);
        }
        catch (Exception ex) when (IsUndefinedColumnCompatibilityError(ex))
        {
            _logger.LogWarning(
                ex,
                "DnevnikPromena query failed due to compatibility error. Falling back to legacy projection.");

            if (query.ArtikalId.HasValue || !string.IsNullOrWhiteSpace(query.Naziv) || !string.Equals(query.DataScope, "all", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Compatibility fallback ignored unsupported filters (ArtikalId/Naziv/DataScope) because required columns are missing.");
            }

            var legacyQuery = BuildLegacyTrendMovementQuery();
            legacyQuery = ApplyFilters(legacyQuery, query, includeArtikalFilters: false);
            legacyQuery = ApplySorting(legacyQuery, normalizedSortBy, normalizedSortDir);

            return await BuildPagedResponseAsync(legacyQuery, pageNumber, pageSize, normalizedSortBy, normalizedSortDir, ct);
        }
    }

    public async Task<DnevnikPromenaDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        TrendMovementProjection? primary;
        try
        {
            primary = await BuildTrendMovementQuery("all")
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "DnevnikPromena detail query hit legacy schema (missing columns). Falling back to compatibility projection.");

            primary = await BuildLegacyTrendMovementQuery()
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex) when (IsUndefinedColumnCompatibilityError(ex))
        {
            _logger.LogWarning(
                ex,
                "DnevnikPromena detail query failed due to compatibility error. Falling back to legacy projection.");

            primary = await BuildLegacyTrendMovementQuery()
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync(ct);
        }

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

    private IQueryable<TrendMovementProjection> BuildLegacyTrendMovementQuery()
    {
        return from dp in _trendDb.DnevnikPromena.AsNoTracking()
               join d in _trendDb.Dobavljaci.AsNoTracking() on dp.DobavljacId equals d.Id into dobavljaci
               from dobavljac in dobavljaci.DefaultIfEmpty()
               select new TrendMovementProjection
               {
                   Id = dp.Id,
                   TipPromene = dp.TipPromene,
                   Datum = dp.Datum,
                   Iznos = dp.Iznos,
                   BrojRacuna = dp.BrojRacuna,
                   ArtikalId = null,
                   ArtikalNaziv = null,
                   DobavljacId = dp.DobavljacId,
                   DobavljacNaziv = dobavljac != null ? dobavljac.Naziv : null,
                   StaraProdajnaCena = null,
                   NovaProdajnaCena = null,
                   Kolicina = null,
                   Komentar = dp.Komentar,
                   KorisnikIme = dp.KorisnikIme,
                   DataOrigin = null
               };
    }

    private static IQueryable<TrendMovementProjection> ApplyFilters(
        IQueryable<TrendMovementProjection> queryable,
        DnevnikPromenaListQuery query,
        bool includeArtikalFilters)
    {
        if (!string.IsNullOrWhiteSpace(query.TipPromene))
            queryable = queryable.Where(x => x.TipPromene == query.TipPromene);

        if (includeArtikalFilters && query.ArtikalId.HasValue)
            queryable = queryable.Where(x => x.ArtikalId == query.ArtikalId.Value);

        if (includeArtikalFilters && !string.IsNullOrWhiteSpace(query.Naziv))
            queryable = queryable.Where(x => x.ArtikalNaziv != null && x.ArtikalNaziv.Contains(query.Naziv));

        if (!string.IsNullOrWhiteSpace(query.BrojRacuna))
            queryable = queryable.Where(x => x.BrojRacuna != null && x.BrojRacuna.Contains(query.BrojRacuna));

        if (query.FromDate.HasValue)
            queryable = queryable.Where(x => x.Datum >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            queryable = queryable.Where(x => x.Datum <= query.ToDate.Value);

        return queryable;
    }

    private static IQueryable<TrendMovementProjection> ApplySorting(
        IQueryable<TrendMovementProjection> queryable,
        string normalizedSortBy,
        string normalizedSortDir)
    {
        return normalizedSortBy switch
        {
            "tippromene" => normalizedSortDir == "asc" ? queryable.OrderBy(x => x.TipPromene) : queryable.OrderByDescending(x => x.TipPromene),
            "iznos" => normalizedSortDir == "asc" ? queryable.OrderBy(x => x.Iznos) : queryable.OrderByDescending(x => x.Iznos),
            "naziv" => normalizedSortDir == "asc" ? queryable.OrderBy(x => x.ArtikalNaziv) : queryable.OrderByDescending(x => x.ArtikalNaziv),
            _ => normalizedSortDir == "asc" ? queryable.OrderBy(x => x.Datum) : queryable.OrderByDescending(x => x.Datum)
        };
    }

    private static async Task<DnevnikPromenaListResponseDto> BuildPagedResponseAsync(
        IQueryable<TrendMovementProjection> dataQuery,
        int pageNumber,
        int pageSize,
        string normalizedSortBy,
        string normalizedSortDir,
        CancellationToken ct)
    {
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

    private static bool IsUndefinedColumnCompatibilityError(Exception ex)
    {
        static bool IsUndefinedColumn(PostgresException pg) =>
            string.Equals(pg.SqlState, PostgresErrorCodes.UndefinedColumn, StringComparison.Ordinal);

        if (ex is PostgresException pgEx && IsUndefinedColumn(pgEx))
            return true;

        var current = ex;
        while (current is not null)
        {
            if (current is PostgresException currentPg && IsUndefinedColumn(currentPg))
                return true;

            if (current.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                && current.Message.Contains("column", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
