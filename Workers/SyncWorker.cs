using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        public SyncWorker(ILogger<SyncWorker> logger, IServiceProvider provider)
        {
            _logger = logger;
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Analytics Sync Worker started (Trendplus -> Analytics). ");

            var delayInterval = TimeSpan.FromSeconds(60);
            const int maxAttempts = 3;

            while (!stoppingToken.IsCancellationRequested)
            {
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
                            _logger.LogInformation("SyncProducts attempt {Attempt} started.", attempt);
                            processed = await SyncProducts(trendplusDb, analyticsDb, stoppingToken);
                            sw.Stop();

                            _logger.LogInformation(
                                "SyncProducts succeeded on attempt {Attempt}. Duration: {DurationMs}ms. Items processed: {Processed}.",
                                attempt,
                                sw.Elapsed.TotalMilliseconds,
                                processed
                            );

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
                            }
                        }
                    }

                    if (!success)
                    {
                        _logger.LogWarning("Sync iteration completed without success after {MaxAttempts} attempts.", maxAttempts);
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
                }
            }

            _logger.LogInformation("Analytics Sync Worker stopped.");
        }

        private static void MapToDim(Domain.Model.Artikli p, ProductsDim dim)
        {
            dim.ProductId = p.Id;
            dim.ProductName = p.Naziv;

            dim.FootwearTypeId = p.IDTipObuce;
            dim.SupplierId = p.IDDobavljac;
            dim.SeasonId = p.IDSezona;

            dim.PurchasePrice = p.NabavnaCena;
            dim.PurchasePriceRsd = p.NabavnaCenaDin;
            dim.FirstSalePrice = p.PrvaProdajnaCena;
            dim.SalePrice = p.ProdajnaCena;

            dim.IsActive = true;
            dim.Timestamp = p.UpdatedAt;
        }

        private async Task<int> SyncProducts(TrendplusDbContext trendplusDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            // watermark: last synced timestamp in analytics
            var lastSynced = await analyticsDb.ProductsDim
                .AsNoTracking()
                .MaxAsync(x => (DateTime?)x.Timestamp, ct) ?? DateTime.MinValue;

            var products = await trendplusDb.Artikli
                .AsNoTracking()
                .Where(p => p.UpdatedAt > lastSynced)
                .ToListAsync(ct);

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
    }
}
