namespace Domain.Model.Povracaj
{
    /// <summary>
    /// Stavka zapisnika o povraćaju - pojedina?an artikal koji se vra?a
    /// </summary>
    public class PovracajStavka : Domain.Model.IAccessImportSourceLineage
    {
        public int Id { get; set; }

        /// <summary>
        /// ID zaglavlja povraćaja kome ova stavka pripada
        /// </summary>
        public int IdPovracaj { get; set; }

        /// <summary>
        /// ID artikla koji se vra?a
        /// </summary>
        public int IdArtikal { get; set; }

        /// <summary>
        /// Koli?ina koja se vra?a
        /// </summary>
        public int Kolicina { get; set; }

        /// <summary>
        /// Cena po komadu (nabavna cena)
        /// </summary>
        public decimal Cena { get; set; }

        /// <summary>
        /// Razlog vra?anja ovog artikla (opciono)
        /// </summary>
        public string? Razlog { get; set; }

        /// <summary>
        /// Stanje artikla: Ošte?eno, Pogrešna veli?ina, Neprodat, Dobar, itd.
        /// </summary>
        public string? StanjeArtikla { get; set; }
        public string? SourceTableKey { get; set; }
        public long? SourceRowId { get; set; }
        public System.DateTime? SourceUpdatedAtUtc { get; set; }
        public string? SourceHash { get; set; }
        public long? SourceBatchId { get; set; }

        /// <summary>
        /// Navigaciona property ka zaglavlju
        /// </summary>
        public PovracajZaglavlje Povracaj { get; set; } = null!;
    }
}
