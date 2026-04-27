using Domain.Model;

namespace Domain.Model.Prodaja;

/// <summary>
/// Zaglavlje prodaje (ra?un)
/// </summary>
public sealed class ProdajaZaglavlje : IAccessImportSourceLineage
{
    public int Id { get; set; }
    public string? BrojRacuna { get; set; }
    public DateTime DatumProdaje { get; set; }
    public string? NacinPlacanja { get; set; }
    public int? IDObjekat { get; set; }
    public string? KorisnikIme { get; set; }    // cashier / operator who processed the sale
    public string DataOrigin { get; set; } = "existing";
    public string? SourceTableKey { get; set; }
    public long? SourceRowId { get; set; }
    public DateTime? SourceUpdatedAtUtc { get; set; }
    public string? SourceHash { get; set; }
    public long? SourceBatchId { get; set; }
    
    // Navigation property
    public ICollection<ProdajaStavka> Stavke { get; set; } = new List<ProdajaStavka>();
}
