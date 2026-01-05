namespace Trendplus.POS.Dtos
{
    public sealed class PosSaleRequest
    {
        public List<PosSaleItem> Items { get; set; } = new();
        public string PaymentType { get; set; } = "cash"; // cash | card
    }
}
