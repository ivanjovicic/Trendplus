using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Commands.UpdateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Artikli.Queries.GetArtikal;
using Application.Artikli.Queries.VratiArtikle;
using Application.Dobavljaci.Queries;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.Prodaja.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Trendplus2.Endpoints;

public static class AllEndpoints
{
    public static void MapAllEndpoints(this WebApplication app)
    {
        // ============ DNEVNIK PROMENA ============
        
        app.MapGet("/api/dnevnik-promena/tipovi", async (ITrendplusDbContext db, CancellationToken ct) =>
        {
            var tipovi = await db.DnevnikPromena
                .Select(x => x.TipPromene)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(ct);

            return Results.Ok(tipovi);
        });

        app.MapGet("/api/dnevnik-promena", async (
            ITrendplusDbContext db,
            int pageNumber = 1,
            int pageSize = 50,
            string? tipPromene = null,
            int? artikalId = null,
            string? naziv = null,
            string? brojRacuna = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string sortBy = "datum",
            string sortDir = "desc",
            CancellationToken ct = default) =>
        {
            try
            {
                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var query = from dp in db.DnevnikPromena.AsNoTracking()
                            join a in db.Artikli.AsNoTracking() on dp.ArtikalId equals a.Id into artikli
                            from artikal in artikli.DefaultIfEmpty()
                            join d in db.Dobavljaci.AsNoTracking() on dp.DobavljacId equals d.Id into dobavljaci
                            from dobavljac in dobavljaci.DefaultIfEmpty()
                            select new
                            {
                                dp.Id,
                                dp.TipPromene,
                                dp.Datum,
                                dp.Iznos,
                                dp.BrojRacuna,
                                dp.ArtikalId,
                                ArtikalNaziv = artikal != null ? artikal.Naziv : null,
                                dp.DobavljacId,
                                DobavljacNaziv = dobavljac != null ? dobavljac.Naziv : null,
                                dp.StaraProdajnaCena,
                                dp.NovaProdajnaCena,
                                dp.Komentar,
                                dp.KorisnikIme
                            };

                if (!string.IsNullOrWhiteSpace(tipPromene))
                    query = query.Where(x => x.TipPromene == tipPromene);

                if (artikalId.HasValue)
                    query = query.Where(x => x.ArtikalId == artikalId.Value);

                if (!string.IsNullOrWhiteSpace(naziv))
                    query = query.Where(x => x.ArtikalNaziv != null && x.ArtikalNaziv.Contains(naziv));

                if (!string.IsNullOrWhiteSpace(brojRacuna))
                    query = query.Where(x => x.BrojRacuna != null && x.BrojRacuna.Contains(brojRacuna));

                if (fromDate.HasValue)
                    query = query.Where(x => x.Datum >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(x => x.Datum <= toDate.Value);

                query = sortBy.ToLower() switch
                {
                    "tippromene" => sortDir == "asc" ? query.OrderBy(x => x.TipPromene) : query.OrderByDescending(x => x.TipPromene),
                    "iznos" => sortDir == "asc" ? query.OrderBy(x => x.Iznos) : query.OrderByDescending(x => x.Iznos),
                    "naziv" => sortDir == "asc" ? query.OrderBy(x => x.ArtikalNaziv) : query.OrderByDescending(x => x.ArtikalNaziv),
                    _ => sortDir == "asc" ? query.OrderBy(x => x.Datum) : query.OrderByDescending(x => x.Datum)
                };

                var total = await query.CountAsync(ct);
                var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

                return Results.Ok(new { items, totalCount = total, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri u?itavanju dnevnika promena");
            }
        });

        // ============ ARTIKLI ============

        app.MapGet("/artikli", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new GetArtikliQuery();
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        });

        app.MapGet("/api/artikli", async (
            ITrendplusDbContext db,
            int pageNumber = 1,
            int pageSize = 50,
            string? naziv = null,
            decimal? minCena = null,
            decimal? maxCena = null,
            decimal? minKolicina = null,
            decimal? maxKolicina = null,
            string sortBy = "naziv",
            string sortDir = "asc",
            CancellationToken ct = default) =>
        {
            try
            {
                var query = db.Artikli.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(naziv))
                    query = query.Where(a => a.Naziv.Contains(naziv));

                if (minCena.HasValue)
                    query = query.Where(a => a.ProdajnaCena >= minCena.Value);

                if (maxCena.HasValue)
                    query = query.Where(a => a.ProdajnaCena <= maxCena.Value);

                if (minKolicina.HasValue)
                    query = query.Where(a => a.Kolicina >= minKolicina.Value);

                if (maxKolicina.HasValue)
                    query = query.Where(a => a.Kolicina <= maxKolicina.Value);

                query = sortBy.ToLower() switch
                {
                    "prodajnacena" => sortDir == "asc" ? query.OrderBy(a => a.ProdajnaCena) : query.OrderByDescending(a => a.ProdajnaCena),
                    "nabavnacena" => sortDir == "asc" ? query.OrderBy(a => a.NabavnaCena) : query.OrderByDescending(a => a.NabavnaCena),
                    "kolicina" => sortDir == "asc" ? query.OrderBy(a => a.Kolicina) : query.OrderByDescending(a => a.Kolicina),
                    "id" => sortDir == "asc" ? query.OrderBy(a => a.Id) : query.OrderByDescending(a => a.Id),
                    _ => sortDir == "asc" ? query.OrderBy(a => a.Naziv) : query.OrderByDescending(a => a.Naziv)
                };

                var total = await query.CountAsync(ct);
                var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

                return Results.Ok(new { items, totalCount = total, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri u?itavanju artikala");
            }
        });

        app.MapGet("/artikli/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var query = new GetArtikalQuery(id);
                var result = await mediator.Send(query, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Artikal nije prona?en" });
            }
        });

        app.MapPost("/artikli", async (CreateArtikalCommand command, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var id = await mediator.Send(command, ct);
                return Results.Ok(new { id });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri kreiranju artikla");
            }
        });

        app.MapPut("/artikli/{id:int}", async (int id, UpdateArtikalCommand command, IMediator mediator, CancellationToken ct) =>
        {
            if (id != command.Id)
                return Results.BadRequest(new { message = "ID ne odgovara" });

            try
            {
                await mediator.Send(command, ct);
                return Results.Ok(new { success = true });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Artikal nije prona?en" });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri ažuriranju artikla");
            }
        });

        // ============ SEZONE ============
        app.MapGet("/api/sezone", async (ITrendplusDbContext db, CancellationToken ct) =>
        {
            var sezone = await db.Sezone.AsNoTracking().OrderBy(s => s.Naziv).ToListAsync(ct);
            return Results.Ok(sezone);
        });

        // ============ DOBAVLJACI ============
        app.MapGet("/api/dobavljaci", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new GetDobavljacQuery();
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        });

        // ============ NIVELACIJE ============
        app.MapPost("/api/nivelacija", async (
            ITrendplusDbContext db,
            ILogger<Program> logger,
            NivelacijaRequest request,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("Nivelacija cene za artikal {ArtikalId}", request.ArtikalId);
                
                var artikal = await db.Artikli.FindAsync(new object[] { request.ArtikalId }, ct);
                if (artikal == null)
                    return Results.NotFound(new { message = "Artikal nije prona?en" });

                var staraCena = artikal.ProdajnaCena;
                artikal.ProdajnaCena = request.NovaProdajnaCena;

                db.DnevnikPromena.Add(new Domain.Model.DnevnikPromena
                {
                    TipPromene = "Nivelacija cena",
                    Datum = DateTime.UtcNow,
                    Iznos = 0,
                    ArtikalId = artikal.Id,
                    StaraProdajnaCena = staraCena,
                    NovaProdajnaCena = request.NovaProdajnaCena,
                    Komentar = request.Komentar,
                    KorisnikIme = "System"
                });

                await db.SaveChangesAsync(ct);
                
                return Results.Ok(new { success = true, message = "Cena uspešno nivelirana" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greška pri nivelaciji cene");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri nivelaciji cene");
            }
        });

        app.MapGet("/api/nivelacije", async (
            ITrendplusDbContext db,
            int pageNumber = 1,
            int pageSize = 50,
            int? artikalId = null,
            string? naziv = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string sortBy = "datum",
            string sortDir = "desc",
            CancellationToken ct = default) =>
        {
            try
            {
                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var query = from dp in db.DnevnikPromena.AsNoTracking()
                            join a in db.Artikli.AsNoTracking() on dp.ArtikalId equals a.Id into artikli
                            from artikal in artikli.DefaultIfEmpty()
                            where dp.TipPromene == "Nivelacija cena"
                            select new
                            {
                                dp.Id,
                                dp.Datum,
                                dp.ArtikalId,
                                ArtikalNaziv = artikal != null ? artikal.Naziv : null,
                                dp.StaraProdajnaCena,
                                dp.NovaProdajnaCena,
                                dp.Komentar,
                                dp.KorisnikIme
                            };

                if (artikalId.HasValue)
                    query = query.Where(x => x.ArtikalId == artikalId.Value);

                if (!string.IsNullOrWhiteSpace(naziv))
                    query = query.Where(x => x.ArtikalNaziv != null && x.ArtikalNaziv.Contains(naziv));

                if (fromDate.HasValue)
                    query = query.Where(x => x.Datum >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(x => x.Datum <= toDate.Value);

                query = sortBy.ToLower() switch
                {
                    "naziv" => sortDir == "asc" ? query.OrderBy(x => x.ArtikalNaziv) : query.OrderByDescending(x => x.ArtikalNaziv),
                    "stara_cena" => sortDir == "asc" ? query.OrderBy(x => x.StaraProdajnaCena) : query.OrderByDescending(x => x.StaraProdajnaCena),
                    "nova_cena" => sortDir == "asc" ? query.OrderBy(x => x.NovaProdajnaCena) : query.OrderByDescending(x => x.NovaProdajnaCena),
                    _ => sortDir == "asc" ? query.OrderBy(x => x.Datum) : query.OrderByDescending(x => x.Datum)
                };

                var total = await query.CountAsync(ct);
                var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

                return Results.Ok(new { items, totalCount = total, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri u?itavanju nivelacija");
            }
        });

        // ============ PRODAJA ============
        app.MapPost("/api/prodaja", async (IMediator mediator, ILogger<Program> logger, ProdajArtikleCommand command, CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("Prodaja artikala");
                await mediator.Send(command, ct);
                return Results.Ok(new { success = true, message = "Prodaja uspešno evidentirana" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greška pri prodaji");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri prodaji");
            }
        });

        app.MapGet("/api/prodaja", async (IMediator mediator, DateTime? fromDate = null, DateTime? toDate = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) =>
        {
            try
            {
                var query = new GetProdajeQuery(fromDate, toDate, pageNumber, pageSize);
                var result = await mediator.Send(query, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri u?itavanju prodaja");
            }
        });
    }
}

// DTO for nivelacija endpoint
public record NivelacijaRequest(int ArtikalId, decimal NovaProdajnaCena, string? Komentar);
