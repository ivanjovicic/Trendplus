namespace Api.Config
{
    /// <summary>
    /// Bound from appsettings.json → "SerpApi" section.
    /// </summary>
    public class SerpApiOptions
    {
        public const string Section = "SerpApi";

        /// <summary>Your SerpAPI key from https://serpapi.com/dashboard</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Amazon marketplace domain, e.g. "amazon.de", "amazon.it"</summary>
        public string AmazonDomain { get; set; } = "amazon.de";

        /// <summary>Max results to read from a single search (SerpAPI page).</summary>
        public int MaxResults { get; set; } = 50;

        /// <summary>HTTP timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 20;
    }
}
