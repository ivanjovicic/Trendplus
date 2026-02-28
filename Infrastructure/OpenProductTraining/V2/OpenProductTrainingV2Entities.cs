using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Infrastructure.OpenProductTraining.V2;

// ------------------------------------------------------------
// NOTE:
// These entities are part of "Open Product Training 2.0" and
// live in Infrastructure (not Domain) because they are explicitly
// storage/ML-infrastructure concerns (pgvector, JSONB artifacts).
// ------------------------------------------------------------

[Table("training_run")]
public sealed class TrainingRun
{
    [Key]
    public long Id { get; set; }

    [MaxLength(200)]
    public string ModelType { get; set; } = "sell_probability_rs";

    public int? DatasetId { get; set; }

    [MaxLength(200)]
    public string FeatureViewName { get; set; } = "vw_feature_store";

    [MaxLength(32)]
    public string Status { get; set; } = "queued";

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? CodeVersion { get; set; }

    public string? ParamsJson { get; set; }
    public string? MetricsJson { get; set; }

    public string? ArtifactUri { get; set; }
    public string? Notes { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
}

[Table("model_version")]
public sealed class ModelVersion
{
    [Key]
    public long Id { get; set; }

    [MaxLength(200)]
    public string ModelType { get; set; } = "sell_probability_rs";

    public int Version { get; set; }

    public long? TrainingRunId { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? OnnxPath { get; set; }
    public string? OnnxSha256 { get; set; }

    public string? FeatureSchemaJson { get; set; }
    public string? MetricsJson { get; set; }
    public string? CalibrationJson { get; set; }
    public string? ShapSummaryJson { get; set; }
    public string? FeatureImportanceJson { get; set; }
    public string? MinFeatureValues { get; set; }
    public string? MaxFeatureValues { get; set; }
    public string? Notes { get; set; }
}

[Table("brand_normalized")]
public sealed class BrandNormalized
{
    [Key]
    public long Id { get; set; }

    public string RawBrand { get; set; } = string.Empty;
    public string NormalizedKey { get; set; } = string.Empty;

    public int? BrandId { get; set; }

    public decimal? Confidence { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("category_normalized")]
public sealed class CategoryNormalized
{
    [Key]
    public long Id { get; set; }

    public string RawCategory { get; set; } = string.Empty;
    public string NormalizedKey { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public decimal? Confidence { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("product_quality_flags")]
public sealed class ProductQualityFlag
{
    [Key]
    public long Id { get; set; }

    public long ProductId { get; set; }

    [MaxLength(200)]
    public string FlagKey { get; set; } = string.Empty;

    public short Severity { get; set; } = 1;
    public string? Details { get; set; }

    public long? TrainingRunId { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("training_label_sell_probability_rs")]
public sealed class TrainingLabelSellProbabilityRs
{
    [Key]
    public long Id { get; set; }

    public long ProductId { get; set; }

    public int HorizonDays { get; set; } = 30;
    public decimal LabelValue { get; set; }

    [MaxLength(50)]
    public string LabelVersion { get; set; } = "v1";

    public DateOnly? AsOfDate { get; set; }
    public DateTime ComputedAt { get; set; }

    public string? Source { get; set; }
    public string? Notes { get; set; }
}

[Table("product_feature_vector_text")]
public sealed class ProductFeatureVectorText
{
    [Key]
    public long Id { get; set; }

    public long ProductId { get; set; }

    [MaxLength(200)]
    public string EmbeddingModel { get; set; } = "e5-small-v2";

    public Vector Embedding { get; set; } = new(new float[256]);
    public Vector? EmbeddingPca64 { get; set; }

    public string? TextHash { get; set; }

    public DateTime CreatedAt { get; set; }
}

[Table("product_feature_vector_image_v2")]
public sealed class ProductFeatureVectorImageV2
{
    [Key]
    public long Id { get; set; }

    public long ProductId { get; set; }

    [MaxLength(200)]
    public string EmbeddingModel { get; set; } = "resnet50-avgpool";

    public Vector Embedding256 { get; set; } = new(new float[256]);
    public Vector? EmbeddingPca64 { get; set; }

    public int? ClusterId { get; set; }
    public decimal? ClusterDistance { get; set; }

    public DateTime CreatedAt { get; set; }
}

[Table("product_feature_vector_price_history")]
public sealed class ProductFeatureVectorPriceHistory
{
    [Key]
    public long Id { get; set; }

    public long ProductId { get; set; }

    [MaxLength(50)]
    public string FeatureVersion { get; set; } = "v1";

    public DateTime ComputedAt { get; set; }

    public string? Currency { get; set; }
    public int? PriceObsCount { get; set; }

    public decimal? Volatility7d { get; set; }
    public decimal? Volatility30d { get; set; }
    public decimal? Volatility90d { get; set; }
    public decimal? Momentum7d { get; set; }
    public decimal? Momentum30d { get; set; }
    public decimal? Momentum90d { get; set; }
    public decimal? DiscountFreq30d { get; set; }
    public decimal? DiscountFreq90d { get; set; }
    public decimal? TypicalChangeRate30d { get; set; }

    public Vector? Vector32 { get; set; }
    public string? Details { get; set; }
}

