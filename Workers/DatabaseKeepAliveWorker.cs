using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application.Artikli.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Workers
{
    public class DatabaseKeepAliveWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseKeepAliveWorker> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(4);

        public DatabaseKeepAliveWorker(IServiceProvider serviceProvider, ILogger<DatabaseKeepAliveWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DatabaseKeepAliveWorker started. Pinging every {Interval} minutes.", _interval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var trendDb = scope.ServiceProvider.GetRequiredService<ITrendplusDbContext>();
                        var analyticsDb = scope.ServiceProvider.GetRequiredService<IAnalyticsDbContext>();

                        _logger.LogDebug("Pinging databases to prevent Neon auto-suspend...");
                        
                        await trendDb.Database.ExecuteSqlRawAsync("SELECT 1", stoppingToken);
                        await analyticsDb.GetDbConnection().CreateCommand().ExecuteScalarAsync(); // IAnalyticsDbContext also needs a ping
                        
                        // Since IAnalyticsDbContext doesn't expose DatabaseFacade directly in the interface, 
                        // we use the connection or a raw command.
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during database keep-alive ping.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
