using System;

namespace Domain.Model
{
    public class DnevnikPromena
    {
        public int Id { get; set; }
        public string TipPromene { get; set; } = string.Empty; // "Unos robe", "Prodaja", "Korekcija", etc.
        public DateTime Datum { get; set; }
        public decimal Iznos { get; set; }
        public string? BrojRacuna { get; set; }
        public int? DobavljacId { get; set; }

        public int? ArtikalId { get; set; }
        public decimal? StaraProdajnaCena { get; set; }
        public decimal? NovaProdajnaCena { get; set; }

        public string? Komentar { get; set; }
        public string? KorisnikIme { get; set; }
    }
}
