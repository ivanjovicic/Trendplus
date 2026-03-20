namespace Api.Models;

public sealed class DnevnikPromenaListItemDto
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
    public string? Komentar { get; init; }
    public string? KorisnikIme { get; init; }
    public string? DataOrigin { get; init; }
}

public sealed class DnevnikPromenaListResponseDto
{
    public IReadOnlyList<DnevnikPromenaListItemDto> Items { get; init; } = Array.Empty<DnevnikPromenaListItemDto>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public string SortBy { get; init; } = "datum";
    public string SortDir { get; init; } = "desc";
}

public sealed class DnevnikPromenaListQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? TipPromene { get; init; }
    public int? ArtikalId { get; init; }
    public string? Naziv { get; init; }
    public string? BrojRacuna { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string SortBy { get; init; } = "datum";
    public string SortDir { get; init; } = "desc";
    public string DataScope { get; init; } = "all";
}
