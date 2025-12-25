using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Artikli.Commands.UpdateArtikal
{
    public class UpdateArtikalHandler : IRequestHandler<UpdateArtikalCommand, Unit>
    {
        private readonly ITrendplusDbContext _db;
        private readonly ILogger<UpdateArtikalHandler> _logger;

        public UpdateArtikalHandler(ITrendplusDbContext db, ILogger<UpdateArtikalHandler> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Unit> Handle(UpdateArtikalCommand request, CancellationToken cancellationToken)
        {
            var entity = await _db.Artikli
                .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
                .ConfigureAwait(false);

            if (entity == null)
            {
                _logger.LogWarning("UpdateArtikal: artikal with Id {Id} not found", request.Id);
                throw new InvalidOperationException($"Artikal sa Id={request.Id} ne postoji.");
            }

            entity.Naziv = request.Naziv;
            entity.IDTipObuce = request.TipObuceId;
            entity.IDDobavljac = request.DobavljacId;
            entity.NabavnaCena = request.NabavnaCena;
            entity.NabavnaCenaDin = request.NabavnaCenaDin;
            entity.PrvaProdajnaCena = request.PrvaProdajnaCena;
            entity.ProdajnaCena = request.ProdajnaCena;
            entity.Kolicina = request.Kolicina ?? entity.Kolicina;
            entity.Komentar = request.Komentar;
            entity.IDObjekat = request.IDObjekat;
            entity.IDSezona = request.IDSezona;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Artikal {Id} updated", request.Id);

            return Unit.Value;
        }
    }
}
