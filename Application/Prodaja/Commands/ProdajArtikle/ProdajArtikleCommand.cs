using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Prodaja.Commands.ProdajArtikle
{
    using MediatR;

    public record ProdajArtikleCommand(
        string BrojRacuna,
        int IdObjekat,
        string NacinPlacanja,
        List<ProdajaStavkaCommandDto> Stavke
    ) : IRequest<int>; // vrati ID prodaje

}
