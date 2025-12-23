using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Prodaja.Commands.ProdajArtikle
{
    public record ProdajaStavkaCommandDto(
     int IdArtikal,
     int Kolicina,
     decimal Cena
 );

}
