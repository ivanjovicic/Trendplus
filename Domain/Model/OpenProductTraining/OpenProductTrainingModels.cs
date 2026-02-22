using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model.OpenProductTraining
{
    [Table("dataset")]
    public class TrainingDataset
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string SourceType { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? License { get; set; }
        public string? RawLocation { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<RawTrainingProduct> RawProducts { get; set; } = new List<RawTrainingProduct>();
        public ICollection<TrainingProduct> Products { get; set; } = new List<TrainingProduct>();
    }

    [Table("raw_product")]
    public class RawTrainingProduct
    {
        [Key]
        public long Id { get; set; }

        public int DatasetId { get; set; }
        public TrainingDataset Dataset { get; set; } = default!;

        [MaxLength(255)]
        public string ExternalId { get; set; } = string.Empty;

        public string RawPayload { get; set; } = "{}";
        public DateTime ImportedAt { get; set; }
    }

    [Table("brand")]
    public class TrainingBrand
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<TrainingProduct> Products { get; set; } = new List<TrainingProduct>();
    }

    [Table("category")]
    public class TrainingCategory
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public TrainingCategory? Parent { get; set; }

        public ICollection<TrainingCategory> Children { get; set; } = new List<TrainingCategory>();
        public ICollection<TrainingProduct> Products { get; set; } = new List<TrainingProduct>();
    }

    [Table("product")]
    public class TrainingProduct
    {
        [Key]
        public long Id { get; set; }

        public int DatasetId { get; set; }
        public TrainingDataset Dataset { get; set; } = default!;

        [MaxLength(255)]
        public string ExternalId { get; set; } = string.Empty;

        public int? BrandId { get; set; }
        public TrainingBrand? Brand { get; set; }

        public int? CategoryId { get; set; }
        public TrainingCategory? Category { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        [MaxLength(50)]
        public string? ShoeType { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        public decimal? Price { get; set; }
        public decimal? AvgRating { get; set; }
        public int? ReviewCount { get; set; }
        public string? MainImageUrl { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public TrainingProductReviewStats? ReviewStats { get; set; }
        public TrainingProductSplit? ProductSplit { get; set; }

        public ICollection<TrainingProductImage> Images { get; set; } = new List<TrainingProductImage>();
        public ICollection<TrainingProductAttribute> Attributes { get; set; } = new List<TrainingProductAttribute>();
        public ICollection<TrainingProductPriceHistory> PriceHistory { get; set; } = new List<TrainingProductPriceHistory>();
        public ICollection<TrainingLabel> TrainingLabels { get; set; } = new List<TrainingLabel>();
        public ICollection<TrainingProductFeatureVector> FeatureVectors { get; set; } = new List<TrainingProductFeatureVector>();
    }

    [Table("product_image")]
    public class TrainingProductImage
    {
        [Key]
        public long Id { get; set; }

        public long ProductId { get; set; }
        public TrainingProduct Product { get; set; } = default!;

        public string? ImageUrl { get; set; }
        public string? LocalPath { get; set; }
        public bool IsPrimary { get; set; }
    }

    [Table("product_attribute")]
    public class TrainingProductAttribute
    {
        [Key]
        public long Id { get; set; }

        public long ProductId { get; set; }
        public TrainingProduct Product { get; set; } = default!;

        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        public string? ValueRaw { get; set; }
        public string? ValueNormalized { get; set; }
    }

    [Table("product_price_history")]
    public class TrainingProductPriceHistory
    {
        [Key]
        public long Id { get; set; }

        public long ProductId { get; set; }
        public TrainingProduct Product { get; set; } = default!;

        [MaxLength(10)]
        public string Currency { get; set; } = "EUR";

        public decimal Price { get; set; }
        public DateTime CollectedAt { get; set; }
    }

    [Table("product_review_stats")]
    public class TrainingProductReviewStats
    {
        [Key]
        public long ProductId { get; set; }
        public TrainingProduct Product { get; set; } = default!;

        public decimal? AvgRating { get; set; }
        public int? RatingCount { get; set; }
        public int? ReviewTextCount { get; set; }
    }

    [Table("training_label")]
    public class TrainingLabel
    {
        [Key]
        public long Id { get; set; }

        public long ProductId { get; set; }
        public TrainingProduct Product { get; set; } = default!;

        [MaxLength(50)]
        public string LabelType { get; set; } = string.Empty;

        public decimal? ValueNumeric { get; set; }
        public string? ValueText { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("product_split")]
    public class TrainingProductSplit
    {
        [Key]
        public long ProductId { get; set; }
        public TrainingProduct Product { get; set; } = default!;

        [MaxLength(10)]
        public string Split { get; set; } = "train";
    }

    [Table("product_feature_vector")]
    public class TrainingProductFeatureVector
    {
        [Key]
        public long Id { get; set; }

        public long ProductId { get; set; }
        public TrainingProduct Product { get; set; } = default!;

        [MaxLength(50)]
        public string FeatureType { get; set; } = string.Empty;

        public int VectorDim { get; set; }
        public byte[] Vector { get; set; } = Array.Empty<byte>();
        public DateTime CreatedAt { get; set; }
    }
}
