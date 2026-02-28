using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model.Analytics
{
    /// <summary>
    /// Daily momentum score per product, computed by comparing today's vs yesterday's snapshot.
    /// </summary>
    [Table("trend_product_momentum")]
    public class TrendProductMomentum
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("snapshot_date")]
        public DateOnly SnapshotDate { get; set; }

        [Required]
        [MaxLength(500)]
        [Column("canonical_key")]
        public string CanonicalKey { get; set; } = string.Empty;

        /// <summary>Weighted score delta in [-1, 1]: 0.7 * score_component + 0.3 * rank_component.</summary>
        [Required]
        [Column("momentum_score")]
        public double MomentumScore { get; set; }

        [Required]
        [Column("score_delta")]
        public double ScoreDelta { get; set; }

        [Required]
        [Column("rank_delta")]
        public int RankDelta { get; set; }

        [Required]
        [Column("is_new_entry")]
        public bool IsNewEntry { get; set; }

        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
