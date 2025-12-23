using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Artikli.Queries.VratiArtikle
{
    using Domain.Model;
    using MediatR;

    public record GetArtikliQuery : IRequest<List<ArtikliDto>>;

}
