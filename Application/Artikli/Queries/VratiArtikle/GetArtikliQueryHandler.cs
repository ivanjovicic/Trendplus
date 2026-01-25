using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Artikli.Queries.VratiArtikle
{
    public class GetArtikliQueryHandler : IRequestHandler<GetArtikliQuery, List<ArtikliDto>>
    {
        private readonly ITrendplusDbContext _db;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "AllArtikliForProdaja";

        public GetArtikliQueryHandler(ITrendplusDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<List<ArtikliDto>> Handle(GetArtikliQuery request, CancellationToken ct)
        {
            if (_cache.TryGetValue(CacheKey, out List<ArtikliDto>? cachedArtikli) && cachedArtikli != null)
            {
                return cachedArtikli;
            }

            var artikli = await _db.Artikli
               .AsNoTracking()
               .Select(a => new ArtikliDto
               {
                   Id = a.Id,
                   Naziv = a.Naziv,
                   ProdajnaCena = a.ProdajnaCena
               })
                .OrderBy(a => a.Naziv)
                .ToListAsync(ct);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

            _cache.Set(CacheKey, artikli, cacheOptions);

            return artikli;
        }
    }
}
