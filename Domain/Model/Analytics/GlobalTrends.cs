using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model.Analytics
{
    /// <summary>
    /// EU fashion trend data from European markets
    /// </summary>
    [Table("EuTrends")]
    public class EuTrend
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [MaxLength(300)]
        public string ProductName { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? Brand { get; set; }
        
        [MaxLength(100)]
        public string? Category { get; set; }
        
        [MaxLength(50)]
        public string? Color { get; set; }
        
        public int? Rank { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Price { get; set; }
        
        [MaxLength(50)]
        public string? Season { get; set; }
        
        [MaxLength(500)]
        public string? ImageUrl { get; set; }
        
        /// <summary>
        /// 512-dimensional vector embedding for similarity matching
        /// </summary>
        [Column(TypeName = "vector(512)")]
        public float[]? Embedding { get; set; }
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Social media trend tracking (TikTok, Instagram, etc.)
    /// </summary>
    [Table("SocialTrends")]
    public class SocialTrend
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string Hashtag { get; set; } = string.Empty;
        
        public int PostsThisMonth { get; set; }
        public int PostsLastMonth { get; set; }
        
        public float? TiktokGrowth { get; set; }
        public float? InstagramGrowth { get; set; }
        public float? PinterestGrowth { get; set; }
        public float? AverageEngagement { get; set; }
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Maps local products to global trends with AI-powered scoring
    /// </summary>
    [Table("GlobalTrendScores")]
    public class GlobalTrendScore
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Reference to local Artikli.Id
        /// </summary>
        [Required]
        public int LocalProductId { get; set; }
        
        [Required]
        [MaxLength(300)]
        public string ProductName { get; set; } = string.Empty;
        
        // Individual scores (0-100)
        public float EuTrendScore { get; set; }
        public float SocialTrendScore { get; set; }
        public float SimilarityScore { get; set; }
        public float ColorScore { get; set; }
        public float PriceScore { get; set; }
        public float SeasonScore { get; set; }
        
        /// <summary>
        /// Weighted average of all scores
        /// </summary>
        public float FinalGlobalScore { get; set; }
        
        public Guid? MatchedEuTrendId { get; set; }
        
        public string[]? MatchedHashtags { get; set; }
        
        [MaxLength(2000)]
        public string? Recommendations { get; set; }
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Historical trend scores for analysis
    /// </summary>
    [Table("TrendHistory")]
    public class TrendHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public int LocalProductId { get; set; }
        
        [Required]
        public DateTime Date { get; set; }
        
        public float FinalGlobalScore { get; set; }
        public float? EuTrendScore { get; set; }
        public float? SocialTrendScore { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
