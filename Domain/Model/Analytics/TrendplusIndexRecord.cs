using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model.Analytics
{
    /// <summary>
    /// Calculated Trendplus Index (0–100) for a scope (market / brand / category / brand_market).
    /// Written daily by the Python pipeline after snapshot + momentum computation.
    /// </summary>
    [Table("trendplus_index")]
    public class TrendplusIndexRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("snapshot_date")]
        public DateOnly SnapshotDate { get; set; }

        /// <summary>"market" | "brand" | "category" | "brand_market"</summary>
        [Required]
        [MaxLength(50)]
        [Column("scope_type")]
        public string ScopeType { get; set; } = string.Empty;

        /// <summary>e.g. "DE", "nike", "sneaker", "nike|de"</summary>
        [Required]
        [MaxLength(200)]
        [Column("scope_value")]
        public string ScopeValue { get; set; } = string.Empty;

        [Required]
        [Column("index_value")]
        public double IndexValue { get; set; }

        [Required]
        [Column("base_component")]
        public double BaseComponent { get; set; }

        [Required]
        [Column("momentum_component")]
        public double MomentumComponent { get; set; }

        [Required]
        [Column("social_component")]
        public double SocialComponent { get; set; }

        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
