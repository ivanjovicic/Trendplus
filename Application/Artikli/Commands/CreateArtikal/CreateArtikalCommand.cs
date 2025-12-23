using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Artikli.Commands.CreateArtikal
{
    public record CreateArtikalCommand(
    string? PLU,
    string Naziv,
    int? IDTipObuce,
    int? IDDobavljac,
    decimal? NabavnaCena,
    decimal? NabavnaCenaDin,
    decimal? PrvaProdajnaCena,
    decimal? ProdajnaCena,
    int? Kolicina,
    string? Komentar,
    int? IDObjekat,
    int? IDSezona
) : IRequest<int>;
}
