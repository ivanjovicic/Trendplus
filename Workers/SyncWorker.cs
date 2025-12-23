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
using System.Text;
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
            _logger.LogInformation("Analytics Sync Worker started.");

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
                        var writeDb = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
                        var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

                        var sw = Stopwatch.StartNew();
                        try
                        {
                            _logger.LogInformation("SyncProducts attempt {Attempt} started.", attempt);
                            processed = await SyncProducts(writeDb, analyticsDb, stoppingToken);
                            sw.Stop();

                            _logger.LogInformation("SyncProducts succeeded on attempt {Attempt}. Duration: {DurationMs}ms. Items processed: {Processed}.",
                                attempt, sw.Elapsed.TotalMilliseconds, processed);

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
                            _logger.LogWarning(ex, "SyncProducts failed on attempt {Attempt}. Duration: {DurationMs}ms.", attempt, sw.Elapsed.TotalMilliseconds);

                            if (attempt < maxAttempts)
                            {
                                var backoffSeconds = Math.Pow(2, attempt); // 2,4,8...
                                var backoff = TimeSpan.FromSeconds(backoffSeconds);
                                _logger.LogInformation("Waiting {Backoff}s before retrying (attempt {Attempt}).", backoffSeconds, attempt + 1);
                                try
                                {
                                    await Task.Delay(backoff, stoppingToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    _logger.LogInformation("Cancellation requested during backoff.");
                                    throw;
                                }
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

                    // Wait between iterations (honors cancellation)
                    try
                    {
                        await Task.Delay(delayInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Analytics Sync Worker stopping (delay canceled).");
                        throw;
                    }
                }
                catch (OperationCanceledException)
                {
                    // graceful shutdown requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in sync worker");
                }
            }

            _logger.LogInformation("Analytics Sync Worker stopped.");
        }

        private async Task<int> SyncProducts(TrendplusDbContext writeDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
        {
            var products = await writeDb.Artikli.AsNoTracking().ToListAsync(ct);

            foreach (var p in products)
            {
                var dim = await analyticsDb.ProductsDim
                    .FirstOrDefaultAsync(x => x.ProductId == p.Id, ct);

                if (dim == null)
                {
                    analyticsDb.ProductsDim.Add(new ProductsDim
                    {
                        ProductId = p.Id,
                        //PLU = p.PLU,
                        ProductName = p.Naziv,

                        //FootwearTypeId = p.IDTipObuce,
                        //SupplierId = p.IDDobavljac,
                        //SeasonId = p.IDSezona,

                        //PurchasePrice = p.NabavnaCena,
                        //PurchasePriceRsd = p.NabavnaCenaDin,
                        //FirstSalePrice = p.PrvaProdajnaCena,
                        //SalePrice = p.ProdajnaCena,

                        IsActive = true
                    });
                }
                else
                {
                    //dim.PLU = p.PLU;
                    dim.ProductName = p.Naziv;

                    //dim.FootwearTypeId = p.IDTipObuce;
                    //dim.SupplierId = p.IDDobavljac;
                    //dim.SeasonId = p.IDSezona;

                    //dim.PurchasePrice = p.NabavnaCena;
                    //dim.PurchasePriceRsd = p.NabavnaCenaDin;
                    //dim.FirstSalePrice = p.PrvaProdajnaCena;
                    //dim.SalePrice = p.ProdajnaCena;
                }
            }

            await analyticsDb.SaveChangesAsync(ct);

            return products.Count;
        }
    

    //private async Task SyncStores(TrendplusDbContext writeDb, AnalyticsDbContext analyticsDb, CancellationToken ct)
    //    {
    //        var stores = await writeDb.Stores.AsNoTracking().ToListAsync(ct);
    //
    //        foreach (var s in stores)
    //        {
    //            var dim = await analyticsDb.StoresDim
    //                .FirstOrDefaultAsync(x => x.StoreId == s.Id, ct);
    //
    //            if (dim == null)
    //            {
    //                analyticsDb.StoresDim.Add(new StoreDim
    //                {
    //                    StoreId = s.Id,
    //                    StoreName = s.StoreName,
    //                    City = s.City,
    //                    Region = s.Region
    //                });
    //            }
    //            else
    //            {
    //                dim.StoreName = s.StoreName;
    //                dim.City = s.City;
    //                dim.Region = s.Region;
    //            }
    //        }
    //
    //        await analyticsDb.SaveChangesAsync(ct);
    //    }
   
    }

    }
