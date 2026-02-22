using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model
{
    /// <summary>
    /// eBay shoe listing fetched via eBay Browse API.
    /// Stored in the Analytics PostgreSQL database.
    /// </summary>
    [Table("ebay_shoe_products")]
    public class EbayShoeProduct
    {
        public int     Id          { get; set; }

        /// <summary>eBay item ID (e.g. "v1|123456789012|0"). Unique key for upserts.</summary>
        public string  EbayItemId  { get; set; } = string.Empty;

        public string? Name        { get; set; }
        public string? Brand       { get; set; }
        public string? Condition   { get; set; }  // "NEW", "USED", "REFURBISHED"

        public decimal? Price          { get; set; }
        public string?  Currency       { get; set; }  // "EUR", "USD" …

        /// <summary>Seller feedback score (0-100).</summary>
        public float  Rating       { get; set; }
        public int    ReviewCount  { get; set; }

        public string? ImageUrl    { get; set; }
        public string? ProductUrl  { get; set; }

        public string? Category    { get; set; }  // the search query type used
        public string? Marketplace { get; set; }  // "EBAY_DE", "EBAY_US" …

        public DateTime LastSynced { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    }
}
