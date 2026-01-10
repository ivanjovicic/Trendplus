using Application.Artikli.Common.Interfaces;
using Domain.Model.Povracaj;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Povracaj.Commands
{
    public class KreirajPovracajHandler : IRequestHandler<KreirajPovracajCommand, KreirajPovracajResponse>
    {
        private readonly ITrendplusDbContext _db;
        private readonly ILogger<KreirajPovracajHandler> _logger;

        public KreirajPovracajHandler(
            ITrendplusDbContext db,
            ILogger<KreirajPovracajHandler> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<KreirajPovracajResponse> Handle(
            KreirajPovracajCommand request,
            CancellationToken cancellationToken)
        {
            // 1. Generiši broj zapisnika
            var brojZapisnika = await GenerisiBrojZapisnika(cancellationToken);

            // 2. Kreiraj zaglavlje
            var now = DateTime.UtcNow;
            var ukupanIznos = request.Stavke.Sum(s => s.Kolicina * s.Cena);

            var zaglavlje = new PovracajZaglavlje
            {
                BrojZapisnika = brojZapisnika,
                DatumPovracaja = now,
                IDDobavljac = request.IDDobavljac,
                RazlogPovracaja = request.RazlogPovracaja,
                Status = "Kreiran",
                UkupanIznos = ukupanIznos,
                Komentar = request.Komentar,
                KreatorKorisnik = "System", // TODO: Uzmi iz konteksta
                DatumKreiranja = now
            };

            // 3. Dodaj stavke
            foreach (var stavkaDto in request.Stavke)
            {
                zaglavlje.Stavke.Add(new PovracajStavka
                {
                    IdArtikal = stavkaDto.IdArtikal,
                    Kolicina = stavkaDto.Kolicina,
                    Cena = stavkaDto.Cena,
                    Razlog = stavkaDto.Razlog,
                    StanjeArtikla = stavkaDto.StanjeArtikla
                });
            }

            _db.PovracajZaglavlja.Add(zaglavlje);
            await _db.SaveChangesAsync(cancellationToken);

            // 4. Dodaj zapis u DnevnikPromena
            var dobavljac = await _db.Dobavljaci.FindAsync(new object[] { request.IDDobavljac }, cancellationToken);
            
            _db.DnevnikPromena.Add(new Domain.Model.DnevnikPromena
            {
                TipPromene = "povra?aj robe",
                Datum = now,
                Iznos = -ukupanIznos, // Negativan iznos jer vra?amo robu
                BrojRacuna = brojZapisnika,
                DobavljacId = request.IDDobavljac,
                Komentar = $"povra?aj dobavlja?u: {dobavljac?.Naziv ?? "N/A"} - {request.RazlogPovracaja}",
                KorisnikIme = "System"
            });

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Kreiran povra?aj {BrojZapisnika} za dobavlja?a {DobavljacId}, ukupno {UkupanIznos} RSD, {BrojStavki} stavki",
                brojZapisnika,
                request.IDDobavljac,
                ukupanIznos,
                request.Stavke.Count
            );

            return new KreirajPovracajResponse(
                zaglavlje.Id,
                brojZapisnika,
                ukupanIznos
            );
        }

        private async Task<string> GenerisiBrojZapisnika(CancellationToken ct)
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"ZP-{year}-";

            // Na?i poslednji broj u ovoj godini
            var lastNumber = await _db.PovracajZaglavlja
                .AsNoTracking()
                .Where(p => p.BrojZapisnika.StartsWith(prefix))
                .Select(p => p.BrojZapisnika)
                .OrderByDescending(b => b)
                .FirstOrDefaultAsync(ct);

            int nextNumber = 1;
            if (lastNumber != null)
            {
                var lastNumPart = lastNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumPart, out var num))
                {
                    nextNumber = num + 1;
                }
            }

            return $"{prefix}{nextNumber:D3}"; // ZP-2026-001
        }
    }
}
