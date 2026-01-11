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
using Domain.Model;

namespace Workers
{
    public class OutboxProcessorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessorWorker> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

        public OutboxProcessorWorker(
            IServiceProvider serviceProvider,
            ILogger<OutboxProcessorWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxProcessorWorker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
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
