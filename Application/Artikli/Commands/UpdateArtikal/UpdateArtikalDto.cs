using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Artikli.Commands.UpdateArtikal
{
    public class UpdateArtikalDto
    {
        public string Naziv { get; set; } = default!;
        public int? TipObuceId { get; set; }
        public int? DobavljacId { get; set; }
        public decimal? NabavnaCena { get; set; }
        public decimal? NabavnaCenaDin { get; set; }
        public decimal? PrvaProdajnaCena { get; set; }
        public decimal ProdajnaCena { get; set; }
        public int? Kolicina { get; set; }
        public string? Komentar { get; set; }
        public int? IDObjekat { get; set; }
        public int? IDSezona { get; set; }
    }
}
