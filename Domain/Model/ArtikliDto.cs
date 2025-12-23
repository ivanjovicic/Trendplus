using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Model
{
    public class ArtikliDto
    {
        public int Id { get; set; }
        public string? PLU { get; set; }
        public string Naziv { get; set; } = string.Empty;

        public decimal? NabavnaCena { get; set; }
        public decimal? NabavnaCenaDin { get; set; }
        public decimal? PrvaProdajnaCena { get; set; }
        public decimal? ProdajnaCena { get; set; }
        public int? Kolicina { get; set; }
        public string? Komentar { get; set; }
    }
}
