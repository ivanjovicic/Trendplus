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
    }
}

