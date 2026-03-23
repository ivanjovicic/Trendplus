using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Domain.Model;
using Microsoft.Extensions.Caching.Memory;

namespace Application.TipObuce.Queries
{
    public class GetTipObuceQueryHandler
        : IRequestHandler<GetTipObuceQuery, List<Domain.Model.TipObuce>>
    {
        private readonly ITrendplusDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "AllTipoviObuce";

        public GetTipObuceQueryHandler(ITrendplusDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<List<Domain.Model.TipObuce>> Handle(GetTipObuceQuery request, CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(CacheKey, out List<Domain.Model.TipObuce>? cached))
            {
                return cached!;
            }

            var tipovi = await _context.TipoviObuce.AsNoTracking().ToListAsync(cancellationToken);

            _cache.Set(CacheKey, tipovi, TimeSpan.FromMinutes(30));

            return tipovi;
        }
    }
}
