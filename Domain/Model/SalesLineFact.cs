using System;

namespace Domain.Model
{
    public class SalesLineFact
    {
        public long Id { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public decimal? NabavnaCena { get; set; }   // purchase price at time of sale (gross margin)
        public string DataOrigin { get; set; } = "existing";

        // Source receipt-line identity (see SalesLineSourceIdentity). Unique per sale
        // together with SourceTableKey; NULL for legacy rows loaded before 2026-10-10.
        public string? SourceTableKey { get; set; }
        public long? SourceLineId { get; set; }
    }
}
