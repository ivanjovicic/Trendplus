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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox messages");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("OutboxProcessorWorker stopped");
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ITrendplusDbContext>();
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

                    // Publish to RabbitMQ if enabled
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
                        _logger.LogWarning(
                            "Message broker is disabled - Message {Id} marked as processed without publishing",
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
    }
}
