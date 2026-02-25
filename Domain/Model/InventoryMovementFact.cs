namespace Domain.Model;

/// <summary>
/// Fact table in the analytics DB — one row per inventory movement event.
/// Populated by SyncWorker from TrendplusDbContext.DnevnikPromena.
/// TipPromene values are defined in <see cref="TipPromeneConstants"/>.
/// </summary>
public class InventoryMovementFact
{
    public long Id { get; set; }

    /// <summary>Source row Id from DnevnikPromena (Trendplus DB)</summary>
    public int SourceId { get; set; }

    public string TipPromene { get; set; } = string.Empty;
    public DateTime Datum { get; set; }

    public int? ArtikalId { get; set; }

    /// <summary>Positive = inflow (ulaz, prenos ulaz); negative = outflow (prenos izlaz)</summary>
    public int? Kolicina { get; set; }

    public decimal? StaraProdajnaCena { get; set; }
    public decimal? NovaProdajnaCena { get; set; }
    public decimal Iznos { get; set; }

    public int? StoreId { get; set; }
    public int? DobavljacId { get; set; }

    public string? BrojDokumenta { get; set; }
    public string? KorisnikIme { get; set; }

    public string DataOrigin { get; set; } = "existing";
}
