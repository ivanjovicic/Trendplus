using Application.Artikli.Common.Interfaces;
using Domain.Model;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Artikli.Queries.GetArtikal
{
    public class GetArtikalHandler : IRequestHandler<GetArtikalQuery, ArtikliDto>
    {
        private readonly ITrendplusDbContext _db;

        public GetArtikalHandler(ITrendplusDbContext db)
        {
            _db = db;
        }

        public async Task<ArtikliDto> Handle(GetArtikalQuery request, CancellationToken ct)
        {
            var entity = await _db.Artikli
                .AsNoTracking()
                .Where(x => x.Id == request.Id)
                .Select(x => new ArtikliDto
                {
                    Id = x.Id,
                    PLU = x.PLU,
                    Naziv = x.Naziv,
                    NabavnaCena = x.NabavnaCena,
                    NabavnaCenaDin = x.NabavnaCenaDin,
                    PrvaProdajnaCena = x.PrvaProdajnaCena,
                    ProdajnaCena = x.ProdajnaCena,
                    Kolicina = x.Kolicina,
                    Komentar = x.Komentar,
                    TipObuceId = x.IDTipObuce,
                    DobavljacId = x.IDDobavljac,
                    IdSezona = x.IDSezona
                })
                .FirstOrDefaultAsync(ct);

            return entity ?? throw new KeyNotFoundException("Artikal nije pronađen.");
        }
    }
}
