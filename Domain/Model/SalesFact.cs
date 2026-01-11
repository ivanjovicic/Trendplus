using System;

namespace Domain.Model
{
    public class SalesFact
    {
        public long Id { get; set; }
        public int SaleId { get; set; }
        public string BrojRacuna { get; set; } = string.Empty;
        public DateTime SaleTimestampUtc { get; set; }
        public int StoreId { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TotalUnits { get; set; }
        public int TotalLines { get; set; }
    }
}
