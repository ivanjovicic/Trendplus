using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api.Models
{
    public sealed class RuntimeScoringEvaluateRequest
    {
        [Required]
        public IFormFile? Image { get; set; }

        public int? ArtikalId { get; set; }
        public decimal? Cost { get; set; }
        public decimal? TargetPrice { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public string? Market { get; set; }

        // Local-data scoring inputs
        public int?    DobavljacId  { get; set; }
        public int?    TipObuceId   { get; set; }
        public int?    SezonaId     { get; set; }
        public string? Velicina     { get; set; }
        public string? Boja         { get; set; }
        public string? Materijal    { get; set; }
    }

    public sealed class RuntimeScoringEvaluateResponse
    {
        public double FinalScore { get; set; }
        public double SellProbabilityRS { get; set; }
        public double PriceFitScore { get; set; }
        public double PopularityScore { get; set; }
        public double DealScore { get; set; }
        public double MarginScore { get; set; }
        public double TrendMomentum { get; set; }
        public string RecommendedPriceRange { get; set; } = string.Empty;

        public double MarketDemandScore { get; set; }
        public double ImageSimilarityScore { get; set; }
        public double SourceCoverageScore { get; set; }
        public int SourceCoverageCount { get; set; }

        // Local-data scores
        public double SupplierScore   { get; set; }
        public double ShoeTypeScore   { get; set; }
        public double SeasonalScore   { get; set; }
        public double SizeColorScore  { get; set; }
        public double MaterialScore   { get; set; }
        public double LocalDemandScore { get; set; }

        public bool HasTrainingSignal { get; set; }

        // Shopify cross-market signals
        public double ShopifyPriceSignalScore { get; set; }
        public int ShopifyMatchCount { get; set; }
        public decimal? ShopifyMedianPrice { get; set; }
        public bool HasShopifySignal { get; set; }

        public bool UsedPythonModel { get; set; }
        public bool UsedOnnxModel { get; set; }
        public string? OnnxModelType { get; set; }
        public int? OnnxModelVersion { get; set; }
        public double? OnnxRawSellProbability { get; set; }
        public double? OnnxSellProbability { get; set; }
        public bool UsedEnterpriseModel { get; set; }
        public double? EnterpriseLinearScore { get; set; }
        public double? EnterpriseRawProbability { get; set; }
        public double? EnterpriseSoftmaxProbability { get; set; }
        public double? EnterpriseCalibratedProbability { get; set; }
        public double? EnterpriseFinalProbability { get; set; }
        public string? EnterpriseModelType { get; set; }
        public int? EnterpriseModelVersion { get; set; }
        public bool EnterpriseModelFromDatabase { get; set; }
        public double? ExternalModelProbability { get; set; }
        public bool UsedFeatureStoreFeatures { get; set; }
        public int? FeatureStoreLocalProductId { get; set; }
        public long? FeatureStoreTrainingProductId { get; set; }
        public string Market { get; set; } = "RS";
        public string? Currency { get; set; }
        public decimal? TypicalPrice { get; set; }

        /// <summary>Short Serbian verdict label, e.g. "Odlično za prodaju"</summary>
        public string Verdict { get; set; } = "Nema podataka";

        /// <summary>Semantic colour key: green | blue | amber | orange | red | gray</summary>
        public string VerdictColor { get; set; } = "gray";

        /// <summary>Single-word quality: "Odlično" / "Dobro" / "Prosečno" / "Rizično" / "Loše"</summary>
        public string ScoreLabel { get; set; } = string.Empty;

        /// <summary>0-100 estimate of how well-supported the prediction is</summary>
        public double Confidence { get; set; }

        /// <summary>Price position vs market median: "ispod_tržišta" | "u_rangu" | "iznad_tržišta" | ""</summary>
        public string PricePositioning { get; set; } = string.Empty;

        /// <summary>Actionable Serbian-language insight strings</summary>
        public List<string> Insights { get; set; } = new();

        public List<RuntimeScoringSimilarProductDto> SimilarProducts { get; set; } = new();
    }

    public sealed class RuntimeScoringSimilarProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public float Similarity { get; set; }
        public string? ImageFileName { get; set; }
        public string? Brand { get; set; }
        public string? ShoeType { get; set; }
    }
}
