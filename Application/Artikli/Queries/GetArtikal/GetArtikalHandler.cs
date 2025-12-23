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
        private readonly IAnalyticsDbContext _db;

        public GetArtikalHandler(IAnalyticsDbContext db)
        {
            _db = db;
        }

        public async Task<ArtikliDto> Handle(GetArtikalQuery request, CancellationToken ct)
        {
            var entity = await _db.ProductsDim
                .AsNoTracking()
                .Where(x => x.ProductId == request.Id)
                .Select(x => new ArtikliDto
                {
                    Id = x.ProductId,
                    //PLU = x.PLU,
                    Naziv = x.ProductName,
                    NabavnaCena = x.PurchasePrice,
                    NabavnaCenaDin = x.PurchasePriceRsd,
                    PrvaProdajnaCena = x.FirstSalePrice,
                    ProdajnaCena = x.SalePrice,
                    //Kolicina = ?, // not available in ProductsDim
                    //Komentar = ?
                })
                .FirstOrDefaultAsync(ct);

            return entity ?? throw new KeyNotFoundException("Artikal nije pronađen.");
        }
    }

}
