using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Model.Prodaja
{
    public record ProdajaStavkaDto(
    int IdArtikal,
    int Kolicina,
    decimal Cena
);
}
