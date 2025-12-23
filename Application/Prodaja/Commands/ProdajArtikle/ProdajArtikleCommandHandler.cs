using Application.Common.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Prodaja.Commands.ProdajArtikle
{
    public class ProdajArtikleCommandHandler
     : IRequestHandler<ProdajArtikleCommand, int>
    {
        private readonly IProdajaRepository _repo;

        public ProdajArtikleCommandHandler(IProdajaRepository repo)
        {
            _repo = repo;
        }

        public async Task<int> Handle(
            ProdajArtikleCommand request,
            CancellationToken cancellationToken)
        {
            return await _repo.ProdajAsync(request, cancellationToken);
        }
    }

}
