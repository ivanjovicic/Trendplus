namespace Api.Services
{
    /// <summary>
    /// Shared weighted trend score formula used by Amazon, eBay and Google Shopping sync services.
    ///
    /// Base score = bayesianSmoothedRating * log10(reviewCount + 2) * priceFactor
    ///
    /// Runtime score additionally uses:
    /// - popularityPriorScore (0-100) from open_product_training labels
    /// - dealScore (0-100) from relative discount vs typical brand+shoe-type price
    /// </summary>
    internal static class ShoeScoring
    {
        public static float Compute(
            float rating,
            int reviewCount,
            decimal? price,
            decimal popularityPriorScore = 0m,
            decimal dealScore = 0m)
        {
            // Bayesian-smoothed rating (stabilizes small-review items without changing the overall score scale).
            reviewCount = Math.Max(reviewCount, 0);
            const float PRIOR_MEAN = 3.8f;
            const float PRIOR_WEIGHT = 10f;
            var observedRating = rating <= 0 ? PRIOR_MEAN : rating;
            var smoothedRating = (observedRating * reviewCount + PRIOR_MEAN * PRIOR_WEIGHT) / (reviewCount + PRIOR_WEIGHT);

            // Popularity component: rating * log10(reviews + 2)
            var popularity = smoothedRating * (float)Math.Log10(reviewCount + 2);

            // Price factor (EUR-centric; symmetric penalty outside sweet spot)
            var priceFactor = 1.0f;
            if (price.HasValue)
            {
                var p = (float)price.Value;
                priceFactor = p < 15f ? 0.55f
                    : p < 40f ? 0.80f
                    : p <= 120f ? 1.15f
                    : p <= 200f ? 1.00f
                    : 0.75f;
            }

            var baseScore = popularity * priceFactor;

            // Conservative runtime boost from open training signals.
            // Max +20% for prior, max +15% for deal quality.
            var priorNorm = Clamp01((float)(popularityPriorScore / 100m));
            var dealNorm = Clamp01((float)(dealScore / 100m));
            var multiplier = 1.0f + (0.20f * priorNorm) + (0.15f * dealNorm);

            return MathF.Round(baseScore * multiplier, 4);
        }

        private static float Clamp01(float value)
            => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
