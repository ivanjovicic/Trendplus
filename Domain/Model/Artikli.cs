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
        public int? Kolicina { get; set; }
        public string? Komentar { get; set; }
        public int? IDObjekat { get; set; }
        public int? IDSezona { get; set; }
    }

}
