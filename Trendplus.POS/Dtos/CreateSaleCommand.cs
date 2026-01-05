namespace Trendplus.POS.Dtos
{
    public sealed class CreateSaleCommand
    {
        public string TerminalId { get; set; } = default!;
        public string Source { get; set; } = "POS";
        public string PaymentType { get; set; } = default!;
        public List<CreateSaleItem> Items { get; set; } = new();
    }
}
