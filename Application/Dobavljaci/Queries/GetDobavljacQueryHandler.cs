using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Domain.Model;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Dobavljaci.Queries
{
    public class GetDobavljacQueryHandler
        : IRequestHandler<GetDobavljacQuery, List<Domain.Model.Dobavljac>>
    {
        private readonly ITrendplusDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "AllDobavljaci";

        public GetDobavljacQueryHandler(ITrendplusDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<List<Domain.Model.Dobavljac>> Handle(GetDobavljacQuery request, CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(CacheKey, out List<Domain.Model.Dobavljac>? cached))
            {
                return cached!;
            }

            var dobavljaci = await _context.Dobavljaci.AsNoTracking().ToListAsync(cancellationToken);

            _cache.Set(CacheKey, dobavljaci, TimeSpan.FromMinutes(30));

            return dobavljaci;
        }
    }
}
