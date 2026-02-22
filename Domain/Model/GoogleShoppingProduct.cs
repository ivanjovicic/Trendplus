using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model
{
    /// <summary>
    /// Google Shopping listing fetched via SerpAPI (engine=google_shopping).
    /// Stored in the Analytics PostgreSQL database.
    /// </summary>
    [Table("google_shopping_products")]
    public class GoogleShoppingProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>SerpAPI product_id — unique across Google Shopping results.</summary>
        [MaxLength(200)]
        public string? ProductId { get; set; }

        [MaxLength(600)]
        public string? Title { get; set; }

        [MaxLength(200)]
        public string? Brand { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? Price { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        public float Rating { get; set; }

        public int ReviewCount { get; set; }

        /// <summary>Google rank position within the search results page.</summary>
        public int Position { get; set; }

        [MaxLength(2000)]
        public string? ImageUrl { get; set; }

        [MaxLength(2000)]
        public string? ProductUrl { get; set; }

        /// <summary>Shoe type / category used as the search query, e.g. "sneakers".</summary>
        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>Gender segment used during sync: "men", "women", "unisex", or null.</summary>
        [MaxLength(20)]
        public string? Gender { get; set; }

        /// <summary>Google Shopping locale / domain queried, e.g. "google.de".</summary>
        [MaxLength(50)]
        public string? Domain { get; set; }

        /// <summary>Computed trend score = rating × log₁₀(reviews+2) × priceFactor. Updated on sync.</summary>
        public float TrendScore { get; set; }

        public DateTime LastSynced { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    }
}
