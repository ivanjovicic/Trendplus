using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Domain.Model
{
    public class Artikli
    {
        [Key]
        public int Id { get; set; }
        public string? PLU { get; set; }
        public string Naziv { get; set; } = string.Empty;
        public int? IDTipObuce { get; set; }
        public int? IDDobavljac { get; set; }
        public decimal? NabavnaCena { get; set; }
        public decimal? NabavnaCenaDin { get; set; }
        public decimal? PrvaProdajnaCena { get; set; }
        public decimal? ProdajnaCena { get; set; }
        
        // Dodatna polja za cipele
        public string? Velicina { get; set; }   // Veličina cipela (npr. "42", "43", "EU 42")
        public string? Boja { get; set; }       // Boja cipela (npr. "Crna", "Braon", "Bela")
        
        public int? Kolicina { get; set; }
        public int? MinimalnaKolicina { get; set; } // NEW: Za analytics i reorder suggestions
        public string? Komentar { get; set; }
        public int? IDObjekat { get; set; }
        public int? IDSezona { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // NEW: Kategorije za analitiku
        public string? Kategorija { get; set; } // "Patike", "Cipele", "Sandale", "Čizme", "Ostalo"
        public string? Pol { get; set; } // "Muško", "Žensko", "Dečije", "Unisex"
        public string? Boja { get; set; } // "Crna", "Bela", "Crvena", "Plava", itd.
    }

}
