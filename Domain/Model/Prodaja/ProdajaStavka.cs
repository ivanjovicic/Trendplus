namespace Domain.Model.Prodaja;

/// <summary>
/// Stavka prodaje (artikal na ra?unu)
/// </summary>
public sealed class ProdajaStavka
{
    public int Id { get; set; }
    public int IdProdaja { get; set; }
    public int IdArtikal { get; set; }
    public int Kolicina { get; set; }
    public decimal Cena { get; set; }
    public decimal? NabavnaCena { get; set; }   // purchase price at time of sale (for GM analytics)
    
    // Navigation property
    public ProdajaZaglavlje? Prodaja { get; set; }
}
