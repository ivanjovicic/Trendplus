using System;
using System.Collections.Generic;

namespace Domain.Model.Povracaj
{
    /// <summary>
    /// Zaglavlje zapisnika o povraćaju robe dobavlja?u.
    /// Predstavlja ceo dokument sa osnovnim podacima.
    /// </summary>
    public class PovracajZaglavlje
    {
        public int Id { get; set; }

        /// <summary>
        /// Jedinstveni broj zapisnika (npr. "ZP-2026-001")
        /// </summary>
        public string BrojZapisnika { get; set; } = string.Empty;

        /// <summary>
        /// Datum kada je povraćaj kreiran
        /// </summary>
        public DateTime DatumPovracaja { get; set; }

        /// <summary>
        /// ID dobavlja?a kome se roba vra?a
        /// </summary>
        public int IDDobavljac { get; set; }

        /// <summary>
        /// Razlog povraćaja (ošte?eno, pogrešna veli?ina, neprodat, itd.)
        /// </summary>
        public string? RazlogPovracaja { get; set; }

        /// <summary>
        /// Status dokumenta: Kreiran, Poslat, Prihva?en, Odbijen
        /// </summary>
        public string Status { get; set; } = "Kreiran";

        /// <summary>
        /// Ukupan iznos povraćaja (zbir svih stavki)
        /// </summary>
        public decimal UkupanIznos { get; set; }

        /// <summary>
        /// Dodatni komentar
        /// </summary>
        public string? Komentar { get; set; }

        /// <summary>
        /// Korisnik koji je kreirao dokument
        /// </summary>
        public string? KreatorKorisnik { get; set; }

        /// <summary>
        /// Korisnik koji je odobrio dokument
        /// </summary>
        public string? OdobrioKorisnik { get; set; }

        /// <summary>
        /// Datum kreiranja
        /// </summary>
        public DateTime DatumKreiranja { get; set; }

        /// <summary>
        /// Datum odobrenja (ako je odobren)
        /// </summary>
        public DateTime? DatumOdobrenja { get; set; }

        /// <summary>
        /// Stavke povraćaja (artikli koji se vra?aju)
        /// </summary>
        public List<PovracajStavka> Stavke { get; set; } = new();

        public string DataOrigin { get; set; } = "existing";
    }
}
