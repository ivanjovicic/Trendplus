using System;

namespace Infrastructure.Model
{
    public class StockReservation
    {
        public long Id { get; set; }
        public long TransferId { get; set; }
        public long SkuId { get; set; }
        public decimal Quantity { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
