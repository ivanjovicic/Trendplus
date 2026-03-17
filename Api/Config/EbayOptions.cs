namespace Api.Config
{
    /// <summary>
    /// Configuration for the eBay Browse API.
    /// Bound from <c>appsettings.json → "Ebay"</c>.
    /// </summary>
    public class EbayOptions
    {
        public const string Section = "Ebay";

        // ── OAuth Client Credentials (recommended) ──────────────────────────
        /// <summary>eBay App ID / Client ID from the developer portal.</summary>
        public string ClientId     { get; set; } = string.Empty;

        /// <summary>eBay Cert ID / Client Secret from the developer portal.</summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>eBay Dev ID from the developer portal (informational).</summary>
        public string DevId        { get; set; } = string.Empty;

        /// <summary>
        /// When true uses sandbox endpoints (api.sandbox.ebay.com).
        /// Set false for production.
        /// </summary>
        public bool   IsSandbox    { get; set; }

        // ── Static token override (optional) ────────────────────────────────
        /// <summary>
        /// Pre-obtained Bearer token.  When empty the service fetches one
        /// automatically via Client Credentials flow using ClientId + ClientSecret.
        /// </summary>
        public string OAuthToken   { get; set; } = string.Empty;

        // ── Common settings ─────────────────────────────────────────────────
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
