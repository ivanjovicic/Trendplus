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
        private readonly IOutboxService _outbox;

        public ProdajArtikleCommandHandler(IProdajaRepository repo, IOutboxService outbox)
        {
            _repo = repo;
            _outbox = outbox;
        }

        public async Task<int> Handle(
            ProdajArtikleCommand request,
            CancellationToken cancellationToken)
        {
            var prodajaId = await _repo.ProdajAsync(request, cancellationToken);

            // Publish event to Outbox (will be processed by OutboxProcessorWorker)
            await _outbox.PublishAsync("ProdajaKreirana", new
            {
                ProdajaId = prodajaId,
                BrojRacuna = request.BrojRacuna,
                IdObjekat = request.IdObjekat,
                NacinPlacanja = request.NacinPlacanja,
                Stavke = request.Stavke,
                Timestamp = DateTime.UtcNow
            }, correlationId: $"PRODAJA-{prodajaId}", ct: cancellationToken);

            return prodajaId;
        }
    }

}
