using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Domain.Model;

namespace Workers
{
    public class OutboxProcessorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessorWorker> _logger;
        private readonly WorkerHealthService _healthService;
        private readonly WorkerRuntimeControlService _controlService;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
        
        private const string WorkerName = "OutboxProcessorWorker";

        public OutboxProcessorWorker(
            IServiceProvider serviceProvider,
            ILogger<OutboxProcessorWorker> logger,
            WorkerHealthService healthService,
            WorkerRuntimeControlService controlService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _healthService = healthService;
            _controlService = controlService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxProcessorWorker started");
            _healthService.ReportRunning(WorkerName, "Starting up...");
            var paused = false;
            var pauseCheckInterval = TimeSpan.FromSeconds(5);

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
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
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
                    _healthService.ReportRunning(WorkerName, "Processing messages...");
                    await ProcessOutboxMessagesAsync(stoppingToken);
                    _healthService.ReportHealthy(WorkerName, $"Last check: {DateTime.UtcNow:HH:mm:ss}");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("OutboxProcessorWorker iteration canceled (likely shutdown or transient connection timeout)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox messages");
                    _healthService.ReportError(WorkerName, ex);
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _healthService.ReportStopped(WorkerName, "Graceful shutdown");
            _logger.LogInformation("OutboxProcessorWorker stopped");
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ITrendplusDbContext>();
            var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            var messageBroker = scope.ServiceProvider.GetRequiredService<IMessageBroker>();

            var messages = await db.OutboxMessages
                .Where(m => !m.IsProcessed && m.RetryCount < 5)
                .OrderBy(m => m.CreatedAt)
                .Take(50)
                .ToListAsync(ct);

            if (!messages.Any())
            {
                return;
            }

            _logger.LogInformation("Processing {Count} outbox messages", messages.Count);

            foreach (var message in messages)
            {
                try
                {
                    _logger.LogInformation(
                        "Processing outbox message {Id} - EventType: {EventType}, CorrelationId: {CorrelationId}",
                        message.Id,
                        message.EventType,
                        message.CorrelationId);

                    // 1) Project to analytics DB (read model)
                    await TryProjectToAnalyticsAsync(message, analyticsDb, ct);

                    // 2) Publish to RabbitMQ if enabled
                    if (messageBroker.IsEnabled)
                    {
                        await messageBroker.PublishAsync(
                            message.EventType,
                            message.Payload,
                            routingKey: message.EventType.ToLowerInvariant(),
                            ct: ct);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Message broker disabled - skipping publish for outbox message {Id}",
                            message.Id);
                    }

                    message.IsProcessed = true;
                    message.ProcessedAt = DateTime.UtcNow;

                    _logger.LogInformation("Outbox message {Id} processed successfully", message.Id);
                }
                catch (Exception ex)
                {
                    message.RetryCount++;
                    message.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";

                    _logger.LogError(
                        ex,
                        "Failed to process outbox message {Id} (Retry: {RetryCount}/5)",
                        message.Id,
                        message.RetryCount);
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Processed {Count} outbox messages", messages.Count);
        }

        private static async Task TryProjectToAnalyticsAsync(
            OutboxMessage message,
            AnalyticsDbContext analyticsDb,
            CancellationToken ct)
        {
            if (!string.Equals(message.EventType, "ProdajaKreirana", StringComparison.OrdinalIgnoreCase))
                return;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var payload = JsonSerializer.Deserialize<ProdajaKreiranaEvent>(message.Payload, options);
            if (payload == null)
                return;

            // Idempotency: do nothing if already exists
            var exists = await analyticsDb.SalesFacts.AnyAsync(x => x.SaleId == payload.ProdajaId, ct);
            if (exists)
                return;

            var totalRevenue = payload.Stavke?.Sum(s => (decimal)s.Kolicina * s.Cena) ?? 0m;
            var totalUnits = payload.Stavke?.Sum(s => s.Kolicina) ?? 0;

            var fact = new SalesFact
            {
                SaleId = payload.ProdajaId,
                BrojRacuna = payload.BrojRacuna ?? string.Empty,
                SaleTimestampUtc = payload.Timestamp,
                StoreId = payload.IdObjekat,
                PaymentType = payload.NacinPlacanja ?? string.Empty,
                TotalAmount = totalRevenue,
                TotalUnits = totalUnits,
                TotalLines = payload.Stavke?.Length ?? 0
            };

            analyticsDb.SalesFacts.Add(fact);

            if (payload.Stavke != null)
            {
                foreach (var s in payload.Stavke)
                {
                    analyticsDb.SalesLineFacts.Add(new SalesLineFact
                    {
                        SaleId = payload.ProdajaId,
                        ProductId = s.IdArtikal,
                        Qty = s.Kolicina,
                        UnitPrice = s.Cena,
                        LineTotal = (decimal)s.Kolicina * s.Cena
                    });
                }
            }

            await analyticsDb.SaveChangesAsync(ct);
        }

        private sealed class ProdajaKreiranaEvent
        {
            public int ProdajaId { get; set; }
            public string? BrojRacuna { get; set; }
            public int IdObjekat { get; set; }
            public string? NacinPlacanja { get; set; }
            public ProdajaStavkaEvent[]? Stavke { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private sealed class ProdajaStavkaEvent
        {
            public int IdArtikal { get; set; }
            public int Kolicina { get; set; }
            public decimal Cena { get; set; }
        }
    }
}
