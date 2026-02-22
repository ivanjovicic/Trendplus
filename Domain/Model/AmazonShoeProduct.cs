using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model
{
    /// <summary>
    /// Amazon shoe listing fetched via SerpAPI.
    /// Stored in the Analytics DB alongside other trend data.
    /// </summary>
    [Table("amazon_shoe_products")]
    public class AmazonShoeProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>Amazon Standard Identification Number – unique per product.</summary>
        [Required]
        [MaxLength(20)]
        public string Asin { get; set; } = default!;

        [MaxLength(500)]
        public string? Name { get; set; }

        [MaxLength(200)]
        public string? Brand { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? Price { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? OriginalPrice { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        public float Rating { get; set; }

        public int ReviewCount { get; set; }

        /// <summary>Computed trend score = rating × log10(reviews+2) × priceFactor. Updated on each sync.</summary>
        public float TrendScore { get; set; }

        [MaxLength(2000)]
        public string? ImageUrl { get; set; }

        [MaxLength(2000)]
        public string? ProductUrl { get; set; }

        /// <summary>Shoe type / category used as the search query (e.g. "sneakers", "boots").</summary>
        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>Gender segment: "men", "women", "unisex", or null (unknown).</summary>
        [MaxLength(20)]
        public string? Gender { get; set; }

        /// <summary>Amazon marketplace domain queried (e.g. "amazon.de", "amazon.it").</summary>
        [MaxLength(50)]
        public string? Domain { get; set; }

        public DateTime LastSynced { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    }
}
