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
        
        private const string WorkerName = "SyncWorker";

        public SyncWorker(
            ILogger<SyncWorker> logger, 
            IServiceProvider provider,
            WorkerHealthService healthService,
            WorkerRuntimeControlService controlService)
        {
            _logger = logger;
            _provider = provider;
            _healthService = healthService;
            _controlService = controlService;
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
                    }

                    await Task.Delay(delayInterval, stoppingToken);
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
            var existing = await analyticsDb.SuppliersDim.Where(x => ids.Contains(x.SupplierId)).ToDictionaryAsync(x => x.SupplierId, ct);
            var inserts = 0; var updates = 0;
            foreach (var s in suppliers)
            {
                if (existing.TryGetValue(s.Id, out var dim))
                {
                    dim.Naziv = s.Naziv ?? dim.Naziv; dim.Adresa = s.Adresa; dim.Telefon = s.Telefon;
                    dim.Napomena = s.Napomena; dim.DataOrigin = s.DataOrigin; dim.UpdatedAt = DateTime.UtcNow;
                    updates++;
                }
                else
                {
                    analyticsDb.SuppliersDim.Add(new Domain.Model.SuppliersDim
                    {
                        SupplierId = s.Id, Naziv = s.Naziv ?? string.Empty, Adresa = s.Adresa,
                        Telefon = s.Telefon, Napomena = s.Napomena, DataOrigin = s.DataOrigin, UpdatedAt = DateTime.UtcNow
                    });
                    inserts++;
                }
            }
            await analyticsDb.SaveChangesAsync(ct);
            _logger.LogInformation("SuppliersDim sync: inserts={I}, updates={U}", inserts, updates);
        }

        private async Task SyncSeasonsDim(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            var seasons = await trendplusDb.Sezone.AsNoTracking().ToListAsync(ct);
            if (seasons.Count == 0) return;

            var ids = seasons.Select(x => x.Id).ToArray();
            var existing = await analyticsDb.SeasonsDim.Where(x => ids.Contains(x.SeasonId)).ToDictionaryAsync(x => x.SeasonId, ct);
            var inserts = 0; var updates = 0;
            foreach (var s in seasons)
            {
                if (existing.TryGetValue(s.Id, out var dim))
                {
                    dim.Naziv = s.Naziv;
                    dim.DatumOd = DateTime.SpecifyKind(s.DatumOd, DateTimeKind.Utc);
                    dim.DatumDo = DateTime.SpecifyKind(s.DatumDo, DateTimeKind.Utc);
                    dim.DataOrigin = s.DataOrigin; dim.UpdatedAt = DateTime.UtcNow;
                    updates++;
                }
                else
                {
                    analyticsDb.SeasonsDim.Add(new Domain.Model.SeasonsDim
                    {
                        SeasonId = s.Id, Naziv = s.Naziv,
                        DatumOd = DateTime.SpecifyKind(s.DatumOd, DateTimeKind.Utc),
                        DatumDo = DateTime.SpecifyKind(s.DatumDo, DateTimeKind.Utc),
                        DataOrigin = s.DataOrigin, UpdatedAt = DateTime.UtcNow
                    });
                    inserts++;
                }
            }
            await analyticsDb.SaveChangesAsync(ct);
            _logger.LogInformation("SeasonsDim sync: inserts={I}, updates={U}", inserts, updates);
        }

        private async Task SyncFootwearTypesDim(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            var types = await trendplusDb.TipoviObuce.AsNoTracking().ToListAsync(ct);
            if (types.Count == 0) return;

            var ids = types.Select(x => x.Id).ToArray();
            var existing = await analyticsDb.FootwearTypesDim.Where(x => ids.Contains(x.TypeId)).ToDictionaryAsync(x => x.TypeId, ct);
            var inserts = 0; var updates = 0;
            foreach (var t in types)
            {
                if (existing.TryGetValue(t.Id, out var dim))
                {
                    dim.Naziv = t.Naziv; dim.DataOrigin = t.DataOrigin; dim.UpdatedAt = DateTime.UtcNow;
                    updates++;
                }
                else
                {
                    analyticsDb.FootwearTypesDim.Add(new Domain.Model.FootwearTypesDim
                    {
                        TypeId = t.Id, Naziv = t.Naziv, DataOrigin = t.DataOrigin, UpdatedAt = DateTime.UtcNow
                    });
                    inserts++;
                }
            }
            await analyticsDb.SaveChangesAsync(ct);
            _logger.LogInformation("FootwearTypesDim sync: inserts={I}, updates={U}", inserts, updates);
        }

        private async Task SyncInventoryMovements(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            // Watermark: sync movements newer than the latest already in analytics
            var lastSynced = await analyticsDb.InventoryMovementFacts
                .AsNoTracking()
                .MaxAsync(x => (DateTime?)x.Datum, ct) ?? DateTime.MinValue;

            var movements = await trendplusDb.DnevnikPromena
                .AsNoTracking()
                .Where(x => x.Datum > lastSynced)
                .ToListAsync(ct);

            if (movements.Count == 0)
            {
                _logger.LogInformation("InventoryMovementFacts sync: no new movements since {LastSynced:o}", lastSynced);
                return;
            }

            foreach (var m in movements)
            {
                analyticsDb.InventoryMovementFacts.Add(new Domain.Model.InventoryMovementFact
                {
                    SourceId = m.Id,
                    TipPromene = m.TipPromene,
                    Datum = DateTime.SpecifyKind(m.Datum, DateTimeKind.Utc),
                    ArtikalId = m.ArtikalId,
                    Kolicina = m.Kolicina,
                    StaraProdajnaCena = m.StaraProdajnaCena,
                    NovaProdajnaCena = m.NovaProdajnaCena,
                    Iznos = m.Iznos,
                    StoreId = m.IDObjekat,
                    DobavljacId = m.DobavljacId,
                    BrojDokumenta = m.BrojRacuna,
                    KorisnikIme = m.KorisnikIme,
                    DataOrigin = m.DataOrigin
                });
            }

            await analyticsDb.SaveChangesAsync(ct);
            _logger.LogInformation("InventoryMovementFacts sync: inserted {Count} new movements since {LastSynced:o}", movements.Count, lastSynced);
        }
    }
}
