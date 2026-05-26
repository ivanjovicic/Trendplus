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
    int? IDDobavljac,
    decimal? StockCoverDays,
    string StockCoverStatus,
    string StockCoverStatusLabel,
    decimal? SellThroughRatio,
    string SellThroughStatus,
    string SellThroughStatusLabel,
    decimal SignalConfidencePct,
    bool RecommendationAllowed,
    List<string> ReasonCodes,
    string DataQualityStatus
);
