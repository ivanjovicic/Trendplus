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
    }
}
