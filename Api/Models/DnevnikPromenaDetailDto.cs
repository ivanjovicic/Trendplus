namespace Api.Models;

public sealed class DnevnikPromenaDetailDto
{
    public int Id { get; init; }
    public string TipPromene { get; init; } = string.Empty;
    public DateTime Datum { get; init; }
    public int? ArtikalId { get; init; }
    public string? NazivArtikla { get; init; }
    public int? Kolicina { get; init; }
    public decimal? StaraCena { get; init; }
    public decimal? NovaCena { get; init; }
    public decimal Iznos { get; init; }
    public string? BrojRacuna { get; init; }
    public string? KorisnikIme { get; init; }
    public string? Komentar { get; init; }
    public string? DataOrigin { get; init; }
    public int SourceId { get; init; }
}
