using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Model.Prodaja
{
    public record KreirajProdajuDto(
      string BrojRacuna,
      int IdObjekat,
      string NacinPlacanja,
      List<ProdajaStavkaDto> Stavke
  );
}
