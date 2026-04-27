using System;

namespace Domain.Model
{
    public class DnevnikPromena : IAccessImportSourceLineage
    {
        public int Id { get; set; }
        /// <summary>Movement type. Use <see cref="TipPromeneConstants"/> constants for all comparisons.</summary>
        public string TipPromene { get; set; } = string.Empty;
        public DateTime Datum { get; set; }
        public decimal Iznos { get; set; }
        public string? BrojRacuna { get; set; }
        public int? DobavljacId { get; set; }

        public int? ArtikalId { get; set; }
        public decimal? StaraProdajnaCena { get; set; }
        public decimal? NovaProdajnaCena { get; set; }

        public int? Kolicina { get; set; }      // quantity involved in this change (e.g. pieces sold/received)
        public int? IDObjekat { get; set; }     // store/objekat for movement tracking and per-store analytics
        public int? RedniBroj { get; set; }     // original sequence/line number from source system (audit trail)
        public string? Komentar { get; set; }
        public string? KorisnikIme { get; set; }
        public string DataOrigin { get; set; } = "existing";
        public string? SourceTableKey { get; set; }
        public long? SourceRowId { get; set; }
        public DateTime? SourceUpdatedAtUtc { get; set; }
        public string? SourceHash { get; set; }
        public long? SourceBatchId { get; set; }
    }
}
