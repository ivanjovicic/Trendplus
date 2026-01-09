using System;

namespace Domain.Model
{
    public class OutboxMessage
    {
        public long Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public bool IsProcessed { get; set; }
        public int RetryCount { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CorrelationId { get; set; }
    }
}
