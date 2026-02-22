namespace Api.Config
{
    /// <summary>
    /// Configuration for the eBay Browse API.
    /// Bound from <c>appsettings.json → "Ebay"</c>.
    /// </summary>
    public class EbayOptions
    {
        public const string Section = "Ebay";

        /// <summary>eBay OAuth 2.0 App token (Client Credentials flow).</summary>
        public string OAuthToken   { get; set; } = string.Empty;

        /// <summary>eBay Marketplace ID. Default: EBAY_DE (Germany).</summary>
        public string Marketplace  { get; set; } = "EBAY_DE";

        /// <summary>ISO currency code matching the marketplace.</summary>
        public string Currency     { get; set; } = "EUR";

        /// <summary>Maximum results per search call (max 200 for Browse API).</summary>
        public int    MaxResults   { get; set; } = 50;

        /// <summary>HTTP timeout in seconds.</summary>
        public int    TimeoutSeconds { get; set; } = 20;
    }
}
