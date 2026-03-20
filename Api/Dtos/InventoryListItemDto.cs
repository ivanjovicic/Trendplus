namespace Trendplus2.Dtos;

public sealed record InventoryListItemDto(
    int Id,
    string? PLU,
    string Naziv,
    int? Kolicina,
    int? MinimalnaKolicina,
    decimal? NabavnaCena,
    decimal EstimatedValue,
    int? IDObjekat,
    int? IDDobavljac
);
