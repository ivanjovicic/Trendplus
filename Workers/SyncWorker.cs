using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Workers
{
    public class SyncWorker : BackgroundService
    {
        private readonly ILogger<SyncWorker> _logger;
        private readonly IServiceProvider _provider;
        private readonly WorkerHealthService _healthService;
        private readonly WorkerRuntimeControlService _controlService;
        private readonly WorkerRuntimePolicyService _runtimePolicyService;
        
        private const string WorkerName = "SyncWorker";
        private static readonly TimeSpan InventoryMovementReplayWindow = TimeSpan.FromDays(14);
        private static readonly TimeSpan ReturnFactsReplayWindow = TimeSpan.FromDays(90);

        public SyncWorker(
            ILogger<SyncWorker> logger, 
            IServiceProvider provider,
            WorkerHealthService healthService,
            WorkerRuntimeControlService controlService,
            WorkerRuntimePolicyService runtimePolicyService)
        {
            _logger = logger;
            _provider = provider;
            _healthService = healthService;
            _controlService = controlService;
            _runtimePolicyService = runtimePolicyService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Analytics Sync Worker started (Trendplus -> Analytics). ");
            _healthService.ReportRunning(WorkerName, "Starting up...");

            var delayInterval = TimeSpan.FromSeconds(60);
            var pauseCheckInterval = TimeSpan.FromSeconds(5);
            const int maxAttempts = 3;
            var paused = false;

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_controlService.IsEnabled)
                {
                    if (!paused)
                    {
                        _logger.LogInformation("{WorkerName} paused (global workers switch OFF).", WorkerName);
                        _healthService.ReportStopped(WorkerName, "Pauziran - workers switch je iskljucen.");
                        paused = true;
                    }

                    try
                    {
                        await Task.Delay(pauseCheckInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    continue;
                }

                var policy = await _runtimePolicyService.GetPolicyAsync(WorkerName, stoppingToken);
                var manualRunRequested = false;
                if (!policy.CanRunNow)
                {
                    if (!paused)
                    {
                        _logger.LogInformation("{WorkerName} paused. Reason: {Reason}", WorkerName, policy.PauseReason ?? "Worker policy disabled execution.");
                        _healthService.ReportStopped(WorkerName, policy.PauseReason ?? "Pauziran - worker policy disabled execution.");
                        paused = true;
                    }

                    try
                    {
                        await Task.Delay(pauseCheckInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    continue;
                }

                if (policy.ManualRunRequested && !string.IsNullOrWhiteSpace(policy.ManualRunToken))
                {
                    manualRunRequested = await _runtimePolicyService.TryConsumeManualRunRequestAsync(
                        WorkerName,
                        policy.ManualRunToken,
                        stoppingToken);

                    if (!manualRunRequested)
                    {
                        try
                        {
                            await Task.Delay(pauseCheckInterval, stoppingToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        continue;
                    }
                }

                if (paused)
                {
                    _logger.LogInformation("{WorkerName} resumed (global workers switch ON).", WorkerName);
                    _healthService.ReportRunning(WorkerName, "Nastavljen rad nakon ukljucivanja workers switch-a.");
                    paused = false;
                }

                try
                {
                    int processed = 0;
                    var success = false;

                    for (var attempt = 1; attempt <= maxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
                    {
                        using var scope = _provider.CreateScope();
                        var trendplusDb = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
                        var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

                        var sw = Stopwatch.StartNew();
                        try
                        {
                            _healthService.ReportRunning(WorkerName, $"Syncing products (attempt {attempt})...");
                            _logger.LogInformation("SyncProducts attempt {Attempt} started.", attempt);
                            processed = await SyncProducts(trendplusDb, analyticsDb, stoppingToken);
                            sw.Stop();

                            _logger.LogInformation(
                                "SyncProducts succeeded on attempt {Attempt}. Duration: {DurationMs}ms. Items processed: {Processed}.",
                                attempt,
                                sw.Elapsed.TotalMilliseconds,
                                processed
                            );

                            _healthService.ReportHealthy(WorkerName, $"Synced {processed} products at {DateTime.UtcNow:HH:mm:ss}");
                            success = true;
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogInformation("Cancellation requested during SyncProducts.");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            sw.Stop();
                            _logger.LogWarning(
                                ex,
                                "SyncProducts failed on attempt {Attempt}. Duration: {DurationMs}ms.",
                                attempt,
                                sw.Elapsed.TotalMilliseconds
                            );

                            if (attempt < maxAttempts)
                            {
                                var backoffSeconds = Math.Pow(2, attempt); // 2,4,8...
                                var backoff = TimeSpan.FromSeconds(backoffSeconds);
                                _logger.LogInformation(
                                    "Waiting {Backoff}s before retrying (attempt {Attempt}).",
                                    backoffSeconds,
                                    attempt + 1
                                );
                                await Task.Delay(backoff, stoppingToken);
                            }
                            else
                            {
                                _logger.LogError(ex, "SyncProducts failed after {MaxAttempts} attempts.", maxAttempts);
                                _healthService.ReportError(WorkerName, ex);
                            }
                        }
                    }

                    if (!success)
                    {
                        _logger.LogWarning("Sync iteration completed without success after {MaxAttempts} attempts.", maxAttempts);
                    }

                    // Sync reference dimension tables (independent, no retry wrapper)
                    using (var dimScope = _provider.CreateScope())
                    {
                        var td = dimScope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
                        var ad = dimScope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
                        try { await SyncSuppliersDim(td, ad, stoppingToken); }     catch (Exception ex) { _logger.LogWarning(ex, "SyncSuppliersDim failed."); }
                        try { await SyncSeasonsDim(td, ad, stoppingToken); }       catch (Exception ex) { _logger.LogWarning(ex, "SyncSeasonsDim failed."); }
                        try { await SyncFootwearTypesDim(td, ad, stoppingToken); } catch (Exception ex) { _logger.LogWarning(ex, "SyncFootwearTypesDim failed."); }
                        try { await SyncInventoryMovements(td, ad, stoppingToken); } catch (Exception ex) { _logger.LogWarning(ex, "SyncInventoryMovements failed."); }
                        try { await SyncReturnFacts(td, ad, stoppingToken); }      catch (Exception ex) { _logger.LogWarning(ex, "SyncReturnFacts failed."); }
                    }

                    var delay = manualRunRequested ? pauseCheckInterval : delayInterval;
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in sync worker");
                    _healthService.ReportError(WorkerName, ex);
                }
            }

            _healthService.ReportStopped(WorkerName, "Graceful shutdown");
            _logger.LogInformation("Analytics Sync Worker stopped.");
        }

        private static void MapToDim(Domain.Model.Artikli p, ProductsDim dim)
        {
            dim.ProductId = p.Id;
            dim.ProductName = p.Naziv;
            dim.Category = p.Kategorija ?? string.Empty;
            dim.SubCategory = p.Pol ?? string.Empty;
            dim.FootwearTypeId = p.IDTipObuce;
            dim.SupplierId = p.IDDobavljac;
            dim.SeasonId = p.IDSezona;
            dim.PurchasePrice = p.NabavnaCena;
            dim.PurchasePriceRsd = p.NabavnaCenaDin;
            dim.FirstSalePrice = p.PrvaProdajnaCena;
            dim.SalePrice = p.ProdajnaCena;
            dim.Velicina = p.Velicina;
            dim.Boja = p.Boja;
            dim.Materijal = p.Materijal;
            dim.Kolicina = p.Kolicina;
            dim.PLU = p.PLU;
            dim.IsActive = true;
            dim.Timestamp = p.UpdatedAt;
            dim.DataOrigin = p.DataOrigin;  // propagate "access"/"existing" so cleanup works
        }

        private async Task<int> SyncProducts(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            // watermark: last synced timestamp in analytics
            DateTime lastSynced;
            try
            {
                lastSynced = await analyticsDb.ProductsDim
                    .AsNoTracking()
                    .MaxAsync(x => (DateTime?)x.Timestamp, ct) ?? DateTime.MinValue;
            }
            catch (PostgresException ex) when (
                ex.SqlState == "42P01" &&
                ex.MessageText.Contains("ProductsDim", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "ProductsDim table is missing in analytics DB. Skipping this sync cycle. " +
                    "Check AnalyticsConnection and run startup migrations/initializer.");
                return 0;
            }

            List<Domain.Model.Artikli> products;
            try
            {
                products = await trendplusDb.Artikli
                    .AsNoTracking()
                    .Where(p => p.UpdatedAt > lastSynced)
                    .OrderBy(p => p.UpdatedAt)
                    .ThenBy(p => p.Id)
                    .ToListAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                _logger.LogWarning(
                    "Trendplus schema is missing expected Artikli column ({Message}). " +
                    "Skipping this sync cycle. Run startup initializer/migrations.",
                    ex.MessageText);
                return 0;
            }

            if (products.Count == 0)
            {
                _logger.LogInformation("ProductsDim sync: no changes since {LastSynced:o}", lastSynced);
                return 0;
            }

            var productIds = products.Select(p => p.Id).ToList();

            var existingDims = await analyticsDb.ProductsDim
                .Where(d => productIds.Contains(d.ProductId))
                .ToListAsync(ct);

            var dimByProductId = existingDims.ToDictionary(d => d.ProductId);

            var inserts = 0;
            var updates = 0;

            foreach (var p in products)
            {
                if (!dimByProductId.TryGetValue(p.Id, out var dim))
                {
                    dim = new ProductsDim();
                    MapToDim(p, dim);
                    analyticsDb.ProductsDim.Add(dim);
                    inserts++;
                }
                else
                {
                    MapToDim(p, dim);
                    updates++;
                }
            }

            await analyticsDb.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ProductsDim incremental sync completed. Since: {LastSynced:o}, Total changed: {Total}, Inserts: {Inserts}, Updates: {Updates}",
                lastSynced,
                products.Count,
                inserts,
                updates
            );

            return products.Count;
        }

        private async Task SyncSuppliersDim(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            var suppliers = await trendplusDb.Dobavljaci.AsNoTracking().ToListAsync(ct);
            if (suppliers.Count == 0) return;

            var ids = suppliers.Select(x => x.Id).ToArray();
            var existingIds = (await analyticsDb.SuppliersDim
                .Where(x => ids.Contains(x.SupplierId))
                .Select(x => x.SupplierId)
                .ToListAsync(ct))
                .ToHashSet();
            var inserts = 0; var updates = 0;
            foreach (var s in suppliers)
            {
                if (existingIds.Contains(s.Id))
                {
                    updates++;
                }
                else
                {
                    inserts++;
                    existingIds.Add(s.Id);
                }

                await analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "SuppliersDim" ("SupplierId", "Naziv", "Adresa", "Telefon", "Napomena", "DataOrigin", "UpdatedAt")
                    VALUES ({s.Id}, {s.Naziv ?? string.Empty}, {s.Adresa}, {s.Telefon}, {s.Napomena}, {s.DataOrigin}, {DateTime.UtcNow})
                    ON CONFLICT ("SupplierId") DO UPDATE
                    SET "Naziv" = EXCLUDED."Naziv",
                        "Adresa" = EXCLUDED."Adresa",
                        "Telefon" = EXCLUDED."Telefon",
                        "Napomena" = EXCLUDED."Napomena",
                        "DataOrigin" = EXCLUDED."DataOrigin",
                        "UpdatedAt" = EXCLUDED."UpdatedAt";
                    """, ct);
            }
            _logger.LogInformation("SuppliersDim sync: inserts={I}, updates={U}", inserts, updates);
        }

        private async Task SyncSeasonsDim(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            var seasons = await trendplusDb.Sezone.AsNoTracking().ToListAsync(ct);
            if (seasons.Count == 0) return;

            var ids = seasons.Select(x => x.Id).ToArray();
            var existingIds = (await analyticsDb.SeasonsDim
                .Where(x => ids.Contains(x.SeasonId))
                .Select(x => x.SeasonId)
                .ToListAsync(ct))
                .ToHashSet();
            var inserts = 0; var updates = 0;
            foreach (var s in seasons)
            {
                if (existingIds.Contains(s.Id))
                {
                    updates++;
                }
                else
                {
                    inserts++;
                    existingIds.Add(s.Id);
                }

                var datumOd = DateTime.SpecifyKind(s.DatumOd, DateTimeKind.Utc);
                var datumDo = DateTime.SpecifyKind(s.DatumDo, DateTimeKind.Utc);
                await analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "SeasonsDim" ("SeasonId", "Naziv", "DatumOd", "DatumDo", "DataOrigin", "UpdatedAt")
                    VALUES ({s.Id}, {s.Naziv}, {datumOd}, {datumDo}, {s.DataOrigin}, {DateTime.UtcNow})
                    ON CONFLICT ("SeasonId") DO UPDATE
                    SET "Naziv" = EXCLUDED."Naziv",
                        "DatumOd" = EXCLUDED."DatumOd",
                        "DatumDo" = EXCLUDED."DatumDo",
                        "DataOrigin" = EXCLUDED."DataOrigin",
                        "UpdatedAt" = EXCLUDED."UpdatedAt";
                    """, ct);
            }
            _logger.LogInformation("SeasonsDim sync: inserts={I}, updates={U}", inserts, updates);
        }

        private async Task SyncFootwearTypesDim(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            var types = await trendplusDb.TipoviObuce.AsNoTracking().ToListAsync(ct);
            if (types.Count == 0) return;

            var ids = types.Select(x => x.Id).ToArray();
            var existingIds = (await analyticsDb.FootwearTypesDim
                .Where(x => ids.Contains(x.TypeId))
                .Select(x => x.TypeId)
                .ToListAsync(ct))
                .ToHashSet();
            var inserts = 0; var updates = 0;
            foreach (var t in types)
            {
                if (existingIds.Contains(t.Id))
                {
                    updates++;
                }
                else
                {
                    inserts++;
                    existingIds.Add(t.Id);
                }

                await analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "FootwearTypesDim" ("TypeId", "Naziv", "DataOrigin", "UpdatedAt")
                    VALUES ({t.Id}, {t.Naziv}, {t.DataOrigin}, {DateTime.UtcNow})
                    ON CONFLICT ("TypeId") DO UPDATE
                    SET "Naziv" = EXCLUDED."Naziv",
                        "DataOrigin" = EXCLUDED."DataOrigin",
                        "UpdatedAt" = EXCLUDED."UpdatedAt";
                    """, ct);
            }
            _logger.LogInformation("FootwearTypesDim sync: inserts={I}, updates={U}", inserts, updates);
        }

        private async Task SyncInventoryMovements(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            // Replay a recent window and upsert by SourceId so late-arriving
            // corrections do not get stuck behind a pure append-only watermark.
            var lastSynced = await analyticsDb.InventoryMovementFacts
                .AsNoTracking()
                .MaxAsync(x => (DateTime?)x.Datum, ct) ?? DateTime.MinValue;

            var replayFrom = lastSynced == DateTime.MinValue
                ? DateTime.MinValue
                : lastSynced.Add(-InventoryMovementReplayWindow);

            var movements = await trendplusDb.DnevnikPromena
                .AsNoTracking()
                .Where(x => x.Datum >= replayFrom)
                .OrderBy(x => x.Datum)
                .ToListAsync(ct);

            if (movements.Count == 0)
            {
                _logger.LogInformation("InventoryMovementFacts sync: no movements found in replay window starting {ReplayFrom:o}", replayFrom);
                return;
            }

            var sourceIds = movements.Select(x => x.Id).ToArray();
            var existingIds = (await analyticsDb.InventoryMovementFacts
                .Where(x => sourceIds.Contains(x.SourceId))
                .Select(x => x.SourceId)
                .ToListAsync(ct))
                .ToHashSet();

            var inserts = 0;
            var updates = 0;

            foreach (var m in movements)
            {
                if (existingIds.Contains(m.Id))
                {
                    updates++;
                }
                else
                {
                    inserts++;
                    existingIds.Add(m.Id);
                }

                var datum = DateTime.SpecifyKind(m.Datum, DateTimeKind.Utc);
                await analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "InventoryMovementFacts" ("SourceId", "TipPromene", "Datum", "ArtikalId", "Kolicina", "StaraProdajnaCena", "NovaProdajnaCena", "Iznos", "StoreId", "DobavljacId", "BrojDokumenta", "KorisnikIme", "DataOrigin")
                    VALUES ({m.Id}, {m.TipPromene}, {datum}, {m.ArtikalId}, {m.Kolicina}, {m.StaraProdajnaCena}, {m.NovaProdajnaCena}, {m.Iznos}, {m.IDObjekat}, {m.DobavljacId}, {m.BrojRacuna}, {m.KorisnikIme}, {m.DataOrigin})
                    ON CONFLICT ("SourceId", "DataOrigin") DO UPDATE
                    SET "TipPromene" = EXCLUDED."TipPromene",
                        "Datum" = EXCLUDED."Datum",
                        "ArtikalId" = EXCLUDED."ArtikalId",
                        "Kolicina" = EXCLUDED."Kolicina",
                        "StaraProdajnaCena" = EXCLUDED."StaraProdajnaCena",
                        "NovaProdajnaCena" = EXCLUDED."NovaProdajnaCena",
                        "Iznos" = EXCLUDED."Iznos",
                        "StoreId" = EXCLUDED."StoreId",
                        "DobavljacId" = EXCLUDED."DobavljacId",
                        "BrojDokumenta" = EXCLUDED."BrojDokumenta",
                        "KorisnikIme" = EXCLUDED."KorisnikIme",
                        "DataOrigin" = EXCLUDED."DataOrigin";
                    """, ct);
            }
            _logger.LogInformation(
                "InventoryMovementFacts sync: replayFrom={ReplayFrom:o}, scanned={Scanned}, inserts={Inserts}, updates={Updates}",
                replayFrom,
                movements.Count,
                inserts,
                updates);
        }

        private async Task SyncReturnFacts(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            var lastSourceLineId = await analyticsDb.ReturnFacts
                .AsNoTracking()
                .MaxAsync(x => (int?)x.SourceLineId, ct) ?? 0;

            var lastReturnTimestamp = await analyticsDb.ReturnFacts
                .AsNoTracking()
                .MaxAsync(x => (DateTime?)x.ReturnTimestampUtc, ct) ?? DateTime.MinValue;

            var replayFrom = lastReturnTimestamp == DateTime.MinValue
                ? DateTime.MinValue
                : lastReturnTimestamp.Add(-ReturnFactsReplayWindow);

            var lines = await trendplusDb.PovracajStavke
                .AsNoTracking()
                .Where(x => x.Id > lastSourceLineId || x.Povracaj.DatumPovracaja >= replayFrom)
                .OrderBy(x => x.Id)
                .Include(x => x.Povracaj)
                .ToListAsync(ct);

            if (lines.Count == 0)
            {
                _logger.LogInformation(
                    "ReturnFacts sync: no rows found for SourceLineId>{LastSourceLineId} or replay window starting {ReplayFrom:o}",
                    lastSourceLineId,
                    replayFrom);
                return;
            }

            var sourceLineIds = lines.Select(x => x.Id).ToArray();
            var existing = await analyticsDb.ReturnFacts
                .Where(x => sourceLineIds.Contains(x.SourceLineId))
                .ToDictionaryAsync(x => x.SourceLineId, ct);

            var inserts = 0;
            var updates = 0;

            foreach (var line in lines)
            {
                if (line.Povracaj is null)
                    continue;

                if (!existing.TryGetValue(line.Id, out var fact))
                {
                    fact = new Domain.Model.ReturnFact();
                    analyticsDb.ReturnFacts.Add(fact);
                    inserts++;
                }
                else
                {
                    updates++;
                }

                MapReturnFact(line, fact);
            }

            await analyticsDb.SaveChangesAsync(ct);
            _logger.LogInformation(
                "ReturnFacts sync: replayFrom={ReplayFrom:o}, scanned={Scanned}, inserts={Inserts}, updates={Updates}",
                replayFrom,
                lines.Count,
                inserts,
                updates);
        }

        private static void MapInventoryMovement(Domain.Model.DnevnikPromena movement, Domain.Model.InventoryMovementFact fact)
        {
            fact.SourceId = movement.Id;
            fact.TipPromene = movement.TipPromene;
            fact.Datum = DateTime.SpecifyKind(movement.Datum, DateTimeKind.Utc);
            fact.ArtikalId = movement.ArtikalId;
            fact.Kolicina = movement.Kolicina;
            fact.StaraProdajnaCena = movement.StaraProdajnaCena;
            fact.NovaProdajnaCena = movement.NovaProdajnaCena;
            fact.Iznos = movement.Iznos;
            fact.StoreId = movement.IDObjekat;
            fact.DobavljacId = movement.DobavljacId;
            fact.BrojDokumenta = movement.BrojRacuna;
            fact.KorisnikIme = movement.KorisnikIme;
            fact.DataOrigin = movement.DataOrigin;
        }

        private static void MapReturnFact(Domain.Model.Povracaj.PovracajStavka line, Domain.Model.ReturnFact fact)
        {
            fact.SourceLineId = line.Id;
            fact.ReturnId = line.IdPovracaj;
            fact.ProductId = line.IdArtikal;
            fact.SupplierId = line.Povracaj.IDDobavljac;
            fact.Qty = line.Kolicina;
            fact.UnitCost = line.Cena;
            fact.LineAmount = line.Kolicina * line.Cena;
            fact.ReturnTimestampUtc = DateTime.SpecifyKind(line.Povracaj.DatumPovracaja, DateTimeKind.Utc);
            fact.Status = Infrastructure.Analytics.ReturnFactStatusMapper.Normalize(line.Povracaj.Status);
            fact.HeaderReason = line.Povracaj.RazlogPovracaja;
            fact.LineReason = line.Razlog;
            fact.ItemCondition = line.StanjeArtikla;
            fact.BrojZapisnika = line.Povracaj.BrojZapisnika;
            fact.DataOrigin = line.Povracaj.DataOrigin;
        }
    }
}
