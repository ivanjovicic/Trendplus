namespace Domain.Model.Prodaja;

/// <summary>
/// Zaglavlje prodaje (ra?un)
/// </summary>
public sealed class ProdajaZaglavlje
{
    public int Id { get; set; }
    public string? BrojRacuna { get; set; }
    public DateTime DatumProdaje { get; set; }
    public string? NacinPlacanja { get; set; }
    public int? IDObjekat { get; set; }
    
    // Navigation property
    public ICollection<ProdajaStavka> Stavke { get; set; } = new List<ProdajaStavka>();
}
