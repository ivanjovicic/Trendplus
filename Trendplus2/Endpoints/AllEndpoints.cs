using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Commands.UpdateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Artikli.Queries.GetArtikal;
using Application.Artikli.Queries.VratiArtikle;
using Application.Dobavljaci.Queries;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.Prodaja.Queries;
using Application.TrendShoes;
using Domain.Model.TrendShoes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Trendplus2.Endpoints;

public static class AllEndpoints
{
    public static void MapAllEndpoints(this WebApplication app)
    {
        // ============ ADMIN - RUN ANALYTICS OPTIMIZATION ============

        app.MapPost("/api/admin/run-analytics-optimization", async (
            ITrendplusDbContext db,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("🚀 Starting analytics optimization migration...");

                var connectionString = db.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    return Results.Problem("No connection string available", statusCode: 500);
                }

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct);

                var results = new List<string>();

                // PART 1: Create Indexes
                var indexSql = @"
                    -- Index on ProdajaZaglavlja.DatumProdaje
                    CREATE INDEX IF NOT EXISTS idx_prodaja_datum ON ""ProdajaZaglavlja"" (""DatumProdaje"" DESC);
                    
                    -- Index on ProdajaStavke for JOIN operations
                    CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_prodaja ON ""ProdajaStavke"" (""IdProdaja"");
                    CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_artikal ON ""ProdajaStavke"" (""IdArtikal"");
                    
                    -- Index on Artikli for category/supplier grouping
                    CREATE INDEX IF NOT EXISTS idx_artikli_kategorija ON ""Artikli"" (""Kategorija"");
                    CREATE INDEX IF NOT EXISTS idx_artikli_dobavljac ON ""Artikli"" (""IDDobavljac"");
                    CREATE INDEX IF NOT EXISTS idx_artikli_pol ON ""Artikli"" (""Pol"");
                ";

                await using (var cmd = new NpgsqlCommand(indexSql, connection))
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                    results.Add("✅ Indexes created");
                }

                // PART 2: Create Pre-aggregated Tables
                var tablesSql = @"
                    -- Daily Sales Summary
                    CREATE TABLE IF NOT EXISTS ""AnalyticsDailySummary"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Date"" DATE NOT NULL UNIQUE,
                        ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ""TotalTransactions"" INT NOT NULL DEFAULT 0,
                        ""TotalUnits"" INT NOT NULL DEFAULT 0,
                        ""AvgBasketValue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ""AvgItemPrice"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS idx_daily_summary_date ON ""AnalyticsDailySummary"" (""Date"" DESC);

                    -- Category Summary
                    CREATE TABLE IF NOT EXISTS ""AnalyticsCategorySummary"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Date"" DATE NOT NULL,
                        ""Kategorija"" VARCHAR(100) NOT NULL,
                        ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ""TotalUnits"" INT NOT NULL DEFAULT 0,
                        ""TransactionCount"" INT NOT NULL DEFAULT 0,
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        UNIQUE(""Date"", ""Kategorija"")
                    );
                    CREATE INDEX IF NOT EXISTS idx_category_summary_date ON ""AnalyticsCategorySummary"" (""Date"" DESC);

                    -- Supplier Summary
                    CREATE TABLE IF NOT EXISTS ""AnalyticsSupplierSummary"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Date"" DATE NOT NULL,
                        ""DobavljacId"" INT,
                        ""DobavljacNaziv"" VARCHAR(200),
                        ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ""TotalUnits"" INT NOT NULL DEFAULT 0,
                        ""TransactionCount"" INT NOT NULL DEFAULT 0,
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        UNIQUE(""Date"", ""DobavljacId"")
                    );
                    CREATE INDEX IF NOT EXISTS idx_supplier_summary_date ON ""AnalyticsSupplierSummary"" (""Date"" DESC);

                    -- Gender Summary
                    CREATE TABLE IF NOT EXISTS ""AnalyticsGenderSummary"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Date"" DATE NOT NULL,
                        ""Pol"" VARCHAR(50) NOT NULL,
                        ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ""TotalUnits"" INT NOT NULL DEFAULT 0,
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        UNIQUE(""Date"", ""Pol"")
                    );
                    CREATE INDEX IF NOT EXISTS idx_gender_summary_date ON ""AnalyticsGenderSummary"" (""Date"" DESC);

                    -- Top Products Summary
                    CREATE TABLE IF NOT EXISTS ""AnalyticsTopProducts"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Date"" DATE NOT NULL,
                        ""ProductId"" INT NOT NULL,
                        ""ProductName"" VARCHAR(300),
                        ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ""TotalUnits"" INT NOT NULL DEFAULT 0,
                        ""Rank"" INT NOT NULL DEFAULT 0,
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        UNIQUE(""Date"", ""ProductId"")
                    );
                    CREATE INDEX IF NOT EXISTS idx_top_products_date ON ""AnalyticsTopProducts"" (""Date"" DESC);
                ";

                await using (var cmd = new NpgsqlCommand(tablesSql, connection))
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                    results.Add("✅ Pre-aggregated tables created");
                }

                // PART 3: Initial data population for last 30 days
                var today = DateTime.UtcNow.Date;
                var populatedDays = 0;

                for (int i = 0; i < 30; i++)
                {
                    var date = today.AddDays(-i);

                    // Refresh daily summary
                    var dailySql = @"
                        INSERT INTO ""AnalyticsDailySummary"" (""Date"", ""TotalRevenue"", ""TotalTransactions"", ""TotalUnits"", ""AvgBasketValue"", ""AvgItemPrice"", ""UpdatedAt"")
                        SELECT 
                            @date::DATE,
                            COALESCE(SUM(ps.""Kolicina"" * ps.""Cena""), 0),
                            COUNT(DISTINCT p.""Id""),
                            COALESCE(SUM(ps.""Kolicina""), 0),
                            CASE WHEN COUNT(DISTINCT p.""Id"") > 0 
                                THEN COALESCE(SUM(ps.""Kolicina"" * ps.""Cena""), 0) / COUNT(DISTINCT p.""Id"")
                                ELSE 0 
                            END,
                            CASE WHEN COALESCE(SUM(ps.""Kolicina""), 0) > 0 
                                THEN COALESCE(SUM(ps.""Kolicina"" * ps.""Cena""), 0) / SUM(ps.""Kolicina"")
                                ELSE 0 
                            END,
                            NOW()
                        FROM ""ProdajaZaglavlja"" p
                        JOIN ""ProdajaStavke"" ps ON p.""Id"" = ps.""IdProdaja""
                        WHERE DATE(p.""DatumProdaje"") = @date::DATE
                        ON CONFLICT (""Date"") DO UPDATE SET
                            ""TotalRevenue"" = EXCLUDED.""TotalRevenue"",
                            ""TotalTransactions"" = EXCLUDED.""TotalTransactions"",
                            ""TotalUnits"" = EXCLUDED.""TotalUnits"",
                            ""AvgBasketValue"" = EXCLUDED.""AvgBasketValue"",
                            ""AvgItemPrice"" = EXCLUDED.""AvgItemPrice"",
                            ""UpdatedAt"" = NOW();
                    ";

                    await using (var cmd = new NpgsqlCommand(dailySql, connection))
                    {
                        cmd.Parameters.AddWithValue("date", date);
                        await cmd.ExecuteNonQueryAsync(ct);
                    }

                    populatedDays++;
                }

                results.Add($"✅ Populated {populatedDays} days of analytics data");

                // Get stats
                var statsResults = new Dictionary<string, int>();

                await using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""AnalyticsDailySummary""", connection))
                {
                    statsResults["dailySummaryCount"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
                }

                logger.LogInformation("✅ Analytics optimization completed successfully");

                return Results.Ok(new
                {
                    success = true,
                    message = "Analytics optimization completed",
                    results,
                    stats = statsResults
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to run analytics optimization");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to run analytics optimization"
                );
            }
        });

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
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju dnevnika promena");
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
            IMemoryCache cache,
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

                // Cache total count to avoid heavy queries on every pagination action
                var filterHash = $"{naziv}_{minCena}_{maxCena}_{minKolicina}_{maxKolicina}";
                var cacheKey = $"artikli_count_{filterHash}";
                
                if (!cache.TryGetValue(cacheKey, out int total))
                {
                    total = await query.CountAsync(ct);
                    cache.Set(cacheKey, total, TimeSpan.FromMinutes(2));
                }

                query = sortBy.ToLower() switch
                {
                    "prodajnacena" => sortDir == "asc" ? query.OrderBy(a => a.ProdajnaCena) : query.OrderByDescending(a => a.ProdajnaCena),
                    "nabavnacena" => sortDir == "asc" ? query.OrderBy(a => a.NabavnaCena) : query.OrderByDescending(a => a.NabavnaCena),
                    "kolicina" => sortDir == "asc" ? query.OrderBy(a => a.Kolicina) : query.OrderByDescending(a => a.Kolicina),
                    "id" => sortDir == "asc" ? query.OrderBy(a => a.Id) : query.OrderByDescending(a => a.Id),
                    _ => sortDir == "asc" ? query.OrderBy(a => a.Naziv) : query.OrderByDescending(a => a.Naziv)
                };

                var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

                return Results.Ok(new { items, totalCount = total, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju artikala");
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
                return Results.NotFound(new { message = "Artikal nije pronađen" });
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
                return Results.NotFound(new { message = "Artikal nije pronađen" });
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
                    return Results.NotFound(new { message = "Artikal nije pronađen" });

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
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju nivelacija");
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
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju prodaja");
            }
        });

        app.MapGet("/api/trends/seasonal-images", async (
            [FromServices] PexelsService pexels,
            ILogger<Program> logger) =>
        {
            try
            {
                var query = "women platform sandals fashion";

                // Only Pexels (Unsplash temporarily disabled)
                var pexelsPhotos = await pexels.Search(query, 20);

                var images = new List<TrendImageDto>();

                // Map Pexels with attribution
                images.AddRange(
                    pexelsPhotos.Select((photo, i) =>
                        new TrendImageDto(
                            i + 1,
                            photo.Src.Medium,
                            "pexels",
                            photo.Photographer,
                            photo.PhotographerUrl,
                            photo.Url
                        ))
                );

                // Shuffle for variety
                var shuffled = images.OrderBy(_ => Guid.NewGuid()).Take(20);
                return Results.Ok(shuffled);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Seasonal images FAILED");
                return Results.Problem(
                    title: "Image providers failed",
                    detail: ex.Message
                );
            }
        });

    }

}

// DTO for nivelacija endpoint
public record NivelacijaRequest(int ArtikalId, decimal NovaProdajnaCena, string? Komentar);
