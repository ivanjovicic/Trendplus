using System;
using System.Collections.Generic;

namespace Domain.Transfers
{
    public class Transfer
    {
        public long Id { get; set; }
        public string Status { get; set; } = "draft";
        public long SourceId { get; set; }
        public long DestinationId { get; set; }
        public bool Reserve { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public List<TransferItem> Items { get; set; } = new();
    }

    public class TransferItem
    {
        public long Id { get; set; }
        public long SkuId { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public long TransferId { get; set; }
    }
}
