using Domain.Model;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Artikli.Queries.GetArtikal
{
    public record GetArtikalQuery(int Id) : IRequest<ArtikliDto>;

}
