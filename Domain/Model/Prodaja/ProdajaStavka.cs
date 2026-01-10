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
    
    // Navigation property
    public ProdajaZaglavlje? Prodaja { get; set; }
}
