namespace Infrastructure.Configuration;

public static class OpenTrainingModelCatalog
{
    public const string SellProbabilityRsModelType = "sell_probability_rs";
    public const string EnterpriseScoringModelType = "enterprise_scoring";
    public const string EnterpriseLogitModelType = "enterprise_logit_v1";
    public const string SupplierRankingModelType = "supplier_ranking_v1";

    public const string DefaultFeatureViewName = "vw_product_training_export";
    public const string EnterpriseDefaultFeatureViewName = "vw_feature_store";
    public const string SupplierRankingFeatureViewName = "supplier_training_dataset_v1";

    public const string EnterpriseTrainingScriptPath = "Python/training/train_enterprise_scoring.py";
    public const string SupplierRankingTrainingScriptPath = "Python/training/train_supplier_ranking.py";
    public const string EnterpriseDefaultTargetColumn = "sold";
    public const string EnterpriseDefaultFeatureColumnsCsv =
        "price_fit,margin,popularity,trend_momentum,source_coverage,local_demand,image_similarity,deal_score,supplier_score,season_score";

    public static readonly string[] EnterpriseDefaultFeatureColumns =
    [
        "price_fit",
        "margin",
        "popularity",
        "trend_momentum",
        "source_coverage",
        "local_demand",
        "image_similarity",
        "deal_score",
        "supplier_score",
        "season_score"
    ];

    public static string NormalizeModelType(string? modelType)
    {
        if (string.IsNullOrWhiteSpace(modelType))
            return SellProbabilityRsModelType;

        return modelType.Trim().ToLowerInvariant();
    }

    public static bool IsEnterpriseModelType(string? modelType)
    {
        var normalized = NormalizeModelType(modelType);
        return string.Equals(normalized, EnterpriseScoringModelType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, EnterpriseLogitModelType, StringComparison.OrdinalIgnoreCase);
    }
}
