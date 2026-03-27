using System;
using System.Collections.Generic;

namespace Domain.Transfers
{
    public static class TransferStatuses
    {
        public const string Draft = "draft";
        public const string Confirmed = "confirmed";
        public const string Completed = "completed";
        public const string Cancelled = "cancelled";
    }

    public class Transfer
    {
        public long Id { get; set; }
        public string Status { get; set; } = TransferStatuses.Draft;
        public long SourceId { get; set; }
        public long DestinationId { get; set; }
        public bool Reserve { get; set; }
        public string? Notes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? ConfirmedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? CancelledAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public List<TransferItem> Items { get; set; } = new();
    }

    public class TransferItem
    {
        public long Id { get; set; }
        public long SkuId { get; set; }
        public decimal Quantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal ProcessedQuantity { get; set; }
        public string? Unit { get; set; }
        public long TransferId { get; set; }
    }
}
