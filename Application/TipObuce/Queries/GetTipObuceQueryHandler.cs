using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Domain.Model;

namespace Application.TipObuce.Queries
{
    public class GetTipObuceQueryHandler
        : IRequestHandler<GetTipObuceQuery, List<Domain.Model.TipObuce>>
    {
        private readonly ITrendplusDbContext _context;

        public GetTipObuceQueryHandler(ITrendplusDbContext context)
        {
            _context = context;
        }

        public async Task<List<Domain.Model.TipObuce>> Handle(GetTipObuceQuery request, CancellationToken cancellationToken)
        {
            return await _context.TipoviObuce.ToListAsync(cancellationToken);
        }
    }
}
