using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Domain.Model;

namespace Application.Dobavljaci.Queries
{
    public class GetDobavljacQueryHandler
        : IRequestHandler<GetDobavljacQuery, List<Domain.Model.Dobavljac>>
    {
        private readonly ITrendplusDbContext _context;

        public GetDobavljacQueryHandler(ITrendplusDbContext context)
        {
            _context = context;
        }

        public async Task<List<Domain.Model.Dobavljac>> Handle(GetDobavljacQuery request, CancellationToken cancellationToken)
        {
            return await _context.Dobavljaci.ToListAsync(cancellationToken);
        }
    }
}
