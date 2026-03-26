using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Domain.Model;

namespace Infrastructure.Services
{
    public class OutboxService : IOutboxService
    {
        private readonly ITrendplusDbContext _db;

        public OutboxService(ITrendplusDbContext db)
        {
            _db = db;
        }

        public Task PublishAsync<T>(string eventType, T payload, string? correlationId = null, CancellationToken ct = default)
        {
            var message = new OutboxMessage
            {
                EventType = eventType,
                Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false,
                RetryCount = 0,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString()
            };

            _db.OutboxMessages.Add(message);
            // Note: SaveChangesAsync should be called by the caller in the same transaction

            return Task.CompletedTask;
        }
    }
}
