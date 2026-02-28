namespace Api.Config
{
    /// <summary>
    /// Runtime scoring configuration for image+market evaluation endpoint.
    /// </summary>
    public sealed class RuntimeScoringOptions
    {
        public const string Section = "RuntimeScoring";

        /// <summary>
        /// Base URL of optional Python ML service (predict endpoint).
        /// </summary>
        public string PythonModelBaseUrl { get; set; } = "http://localhost:8000";

        /// <summary>
        /// Relative path to prediction endpoint on Python service.
        /// </summary>
        public string PredictPath { get; set; } = "/predict";

        /// <summary>
        /// Enables calling Python /predict endpoint. If false, only local heuristic scoring is used.
        /// </summary>
        public bool EnablePythonPredict { get; set; }

        /// <summary>
        /// Timeout for Python prediction calls in seconds.
        /// </summary>
        public int PythonTimeoutSeconds { get; set; } = 20;

        /// <summary>
        /// Default market used when request does not specify market.
        /// </summary>
        public string DefaultMarket { get; set; } = "RS";

        /// <summary>
        /// Enterprise-grade scoring layer configuration (linear model + calibration + hybrid blending).
        /// </summary>
        public EnterpriseScoringOptions Enterprise { get; set; } = new();

        /// <summary>
        /// Heuristic scoring constants exposed via config for runtime tuning.
        /// </summary>
        public RuntimeScoringTuningOptions Tuning { get; set; } = new();
    }

    public sealed class RuntimeScoringTuningOptions
    {
        public int MarketplaceCoverageItemsPerUnit { get; set; } = 200;
        public int MarketplaceCoverageMaxUnits { get; set; } = 3;
        public int SourceCoverageNormalizationMaxUnits { get; set; } = 6;

        public double PriceFitExponentialDecay { get; set; } = 3.0;
        public double DealTanhMultiplier { get; set; } = 4.0;

        public double ConfidenceBase { get; set; } = 20.0;
        public double ConfidenceTrainingBonus { get; set; } = 25.0;
        public double ConfidencePerSource { get; set; } = 8.0;
        public double ConfidenceSourceCap { get; set; } = 30.0;
        public double ConfidenceImageDivisor { get; set; } = 5.0;
        public double ConfidenceImageCap { get; set; } = 15.0;
        public double ConfidenceBaselineBonus { get; set; } = 10.0;
        public double ConfidenceCap { get; set; } = 95.0;
    }

    public sealed class EnterpriseScoringOptions
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// If true, runtime tries to load active enterprise scoring parameters from model_version.
        /// </summary>
        public bool PreferModelVersionParameters { get; set; } = true;

        /// <summary>
        /// model_version.model_type used for enterprise linear+Platt parameters.
        /// </summary>
        public string ModelType { get; set; } = "enterprise_scoring";

        /// <summary>
        /// Cache refresh interval for model_version lookup.
        /// </summary>
        public int ModelRefreshSeconds { get; set; } = 60;

        /// <summary>
        /// Logistic linear score bias (intercept).
        /// </summary>
        public double Bias { get; set; } = -4.0;

        /// <summary>
        /// Platt scaling parameters: p = sigmoid(A * z + B).
        /// </summary>
        public double CalibrationA { get; set; } = 1.0;
        public double CalibrationB { get; set; } = 0.0;

        /// <summary>
        /// Softmax temperature for ranking stabilization.
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Blend calibrated probability with softmax-proxy probability.
        /// 0 => calibrated only, 1 => softmax only.
        /// </summary>
        public double SoftmaxBlendWeight { get; set; } = 0.10;

        /// <summary>
        /// Runtime blend when ONNX/Python prediction exists.
        /// </summary>
        public double HeuristicWeightWithExternalModel { get; set; } = 0.20;
        public double ExternalModelWeight { get; set; } = 0.65;
        public double EnterpriseWeightWithExternalModel { get; set; } = 0.15;

        /// <summary>
        /// Runtime blend when no ONNX/Python prediction exists.
        /// </summary>
        public double HeuristicWeightWithoutExternalModel { get; set; } = 0.25;
        public double EnterpriseWeightWithoutExternalModel { get; set; } = 0.75;

        /// <summary>
        /// Optional z-score normalization maps. If missing, raw [0..1] features are used.
        /// </summary>
        public Dictionary<string, double> FeatureMeans { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> FeatureStdDevs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Canonical model weights by feature key.
        /// </summary>
        public Dictionary<string, double> FeatureWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["price_fit"] = 1.15,
            ["margin"] = 0.95,
            ["popularity"] = 1.30,
            ["trend_momentum"] = 0.90,
            ["source_coverage"] = 0.65,
            ["local_demand"] = 0.80,
            ["image_similarity"] = 0.55,
            ["deal_score"] = 0.85,
            ["supplier_score"] = 0.40,
            ["season_score"] = 0.35
        };
    }
}
