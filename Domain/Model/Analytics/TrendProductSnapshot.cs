using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model.Analytics
{
    /// <summary>
    /// Daily snapshot of a product's popularity score and global rank.
    /// One row per canonical_key per day, written by the Python daily pipeline.
    /// </summary>
    [Table("trend_product_snapshots")]
    public class TrendProductSnapshot
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

        [Required]
        [MaxLength(500)]
        [Column("product_name")]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("brand")]
        public string Brand { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("category")]
        public string? Category { get; set; }

        [MaxLength(10)]
        [Column("market")]
        public string? Market { get; set; }

        [Required]
        [Column("score")]
        public double Score { get; set; }

        [Required]
        [Column("rank_global")]
        public int RankGlobal { get; set; }

        [Column("social_score")]
        public double? SocialScore { get; set; }

        [Required]
        [Column("source_count")]
        public int SourceCount { get; set; }

        [Required]
        [Column("unique_sources")]
        public int UniqueSources { get; set; }

        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
