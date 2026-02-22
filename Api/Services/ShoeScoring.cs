namespace Api.Services
{
    /// <summary>
    /// Shared weighted trend score formula used by both Amazon and eBay sync services.
    ///
    /// score = rating × log₁₀(reviewCount + 2) × priceFactor
    ///
    /// priceFactor rewards products in the EUR 40–120 sweet spot and penalises
    /// very cheap (possibly poor quality) or very expensive items.
    /// Typical score range: 0 – ~8.
    /// </summary>
    internal static class ShoeScoring
    {
        public static float Compute(float rating, int reviewCount, decimal? price)
        {
            // Popularity component: rating × log₁₀(reviews + 2)
            // log₁₀(2)≈0.3 for 0 reviews, log₁₀(102)≈2 for 100 reviews
            float popularity = rating * (float)Math.Log10(reviewCount + 2);

            // Price factor (EUR-centric; symmetric penalty outside sweet spot)
            float priceFactor = 1.0f;
            if (price.HasValue)
            {
                float p = (float)price.Value;
                priceFactor = p < 15f   ? 0.55f   // suspiciously cheap
                            : p < 40f   ? 0.80f   // budget
                            : p <= 120f ? 1.15f   // sweet spot ✓
                            : p <= 200f ? 1.00f   // premium but ok
                            : 0.75f;              // luxury / outlier
            }

            return MathF.Round(popularity * priceFactor, 4);
        }
    }
}
