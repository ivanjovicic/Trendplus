using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Artikli.Queries.VratiArtikle
{
    using Application.Artikli.Common.Interfaces;
    using Domain.Model;
    using MediatR;
    using Microsoft.EntityFrameworkCore;

    public class GetArtikliQueryHandler : IRequestHandler<GetArtikliQuery, List<ArtikliDto>>
    {
        private readonly ITrendplusDbContext _db;

        public GetArtikliQueryHandler(ITrendplusDbContext db)
        {
            _db = db;
        }

        public async Task<List<ArtikliDto>> Handle(GetArtikliQuery request, CancellationToken ct)
        {
            return await _db.Artikli
               .Select(a => new ArtikliDto
               {
                   Id = a.Id,
                   Naziv = a.Naziv,
                   ProdajnaCena = a.ProdajnaCena
               })

                .OrderBy(a => a.Naziv)
                .ToListAsync(ct);
        }
    }

}
