namespace Trendplus.POS.Dtos
{
    public sealed class CreateSaleResponse
    {
        public long SaleId { get; set; }
        public decimal Total { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
