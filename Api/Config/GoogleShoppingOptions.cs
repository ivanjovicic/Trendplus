namespace Api.Config
{
    /// <summary>
    /// Configuration for the Google Shopping SerpAPI engine.
    /// Bound from <c>appsettings.json → "GoogleShopping"</c>.
    /// The SerpAPI key is shared with Amazon via <see cref="SerpApiOptions"/>.
    /// </summary>
    public class GoogleShoppingOptions
    {
        public const string Section = "GoogleShopping";

        /// <summary>Google country code for shopping results (gl parameter), e.g. "de", "us", "gb".</summary>
        public string CountryCode { get; set; } = "de";

        /// <summary>Google language code (hl parameter), e.g. "de", "en".</summary>
        public string Language { get; set; } = "de";

        /// <summary>ISO currency code for price display, e.g. "EUR", "USD".</summary>
        public string Currency { get; set; } = "EUR";

        /// <summary>Max results to fetch per search call (up to 100).</summary>
        public int MaxResults { get; set; } = 50;

        /// <summary>HTTP timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 25;
    }
}
