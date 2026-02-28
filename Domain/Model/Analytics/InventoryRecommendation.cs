using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model.Analytics
{
    /// <summary>
    /// Recommended stock order quantity per product per day,
    /// computed from trend_score + momentum_score + sales_velocity.
    /// </summary>
    [Table("inventory_recommendations")]
    public class InventoryRecommendation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("snapshot_date")]
        public DateOnly SnapshotDate { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("product_id")]
        public string ProductId { get; set; } = string.Empty;

        [MaxLength(200)]
        [Column("brand")]
        public string? Brand { get; set; }

        [MaxLength(100)]
        [Column("category")]
        public string? Category { get; set; }

        [Required]
        [Column("sales_velocity")]
        public double SalesVelocity { get; set; }

        [Required]
        [Column("stock_on_hand")]
        public double StockOnHand { get; set; }

        [Required]
        [Column("trend_score")]
        public double TrendScore { get; set; }

        [Required]
        [Column("momentum_score")]
        public double MomentumScore { get; set; }

        [Required]
        [Column("recommended_qty")]
        public int RecommendedQty { get; set; }

        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
