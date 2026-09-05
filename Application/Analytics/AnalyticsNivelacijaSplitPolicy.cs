namespace Application.Analytics;

public readonly record struct NivelacijaSplitSnapshot(
    decimal PreRevenue,
    int PreQuantity,
    decimal PostRevenue,
    int PostQuantity,
    decimal RevenueWithSplit,
    decimal ComparableRevenueWithSplit,
    int ArticleCountWithNivelacija,
    int ComparableArticleCount,
    int ComparablePreQuantity,
    int ComparablePostQuantity,
    double? RevenueCoveragePct,
    double? ComparableRevenueCoveragePct,
    double? RevenueImpactPct,
    double? UnitsImpactPct,
    bool HasComparableSignal,
    string? SignalNote);

public static class AnalyticsNivelacijaSplitPolicy
{
    public const int MinimumComparablePreQuantity = 5;
    public const double MinimumComparableCoveragePct = 15d;

    public static NivelacijaSplitSnapshot Build<T>(
        IEnumerable<T> rows,
        IReadOnlyDictionary<int, DateTime> firstNivelacijaByArticle,
        Func<T, int> articleIdSelector,
        Func<T, DateTime> saleDateSelector,
        Func<T, decimal> revenueSelector,
        Func<T, int> quantitySelector)
    {
        var materializedRows = rows as IReadOnlyCollection<T> ?? rows.ToList();
        var totalRevenue = materializedRows.Sum(revenueSelector);

        decimal preRevenue = 0m;
        decimal postRevenue = 0m;
        decimal comparablePreRevenue = 0m;
        decimal comparablePostRevenue = 0m;
        decimal revenueWithSplit = 0m;
        int preQuantity = 0;
        int postQuantity = 0;
        int comparablePreQuantity = 0;
        int comparablePostQuantity = 0;
        int articleCountWithNivelacija = 0;
        int comparableArticleCount = 0;

        foreach (var articleGroup in materializedRows.GroupBy(articleIdSelector))
        {
            if (!firstNivelacijaByArticle.TryGetValue(articleGroup.Key, out var firstNivelacijaDate))
            {
                continue;
            }

            articleCountWithNivelacija++;

            decimal articlePreRevenue = 0m;
            decimal articlePostRevenue = 0m;
            int articlePreQuantity = 0;
            int articlePostQuantity = 0;
            decimal articleRevenueWithSplit = 0m;

            foreach (var row in articleGroup)
            {
                var revenue = revenueSelector(row);
                var quantity = quantitySelector(row);
                articleRevenueWithSplit += revenue;

                if (saleDateSelector(row) < firstNivelacijaDate)
                {
                    articlePreRevenue += revenue;
                    articlePreQuantity += quantity;
                }
                else
                {
                    articlePostRevenue += revenue;
                    articlePostQuantity += quantity;
                }
            }

            revenueWithSplit += articleRevenueWithSplit;
            preRevenue += articlePreRevenue;
            postRevenue += articlePostRevenue;
            preQuantity += articlePreQuantity;
            postQuantity += articlePostQuantity;

            if (articlePreRevenue <= 0m || articlePostRevenue <= 0m || articlePreQuantity <= 0 || articlePostQuantity <= 0)
            {
                continue;
            }

            comparableArticleCount++;
            comparablePreRevenue += articlePreRevenue;
            comparablePostRevenue += articlePostRevenue;
            comparablePreQuantity += articlePreQuantity;
            comparablePostQuantity += articlePostQuantity;
        }

        var revenueCoveragePct = totalRevenue > 0m
            ? Math.Round((double)(revenueWithSplit / totalRevenue * 100m), 2)
            : (double?)null;

        var comparableRevenueCoveragePct = totalRevenue > 0m
            ? Math.Round((double)((comparablePreRevenue + comparablePostRevenue) / totalRevenue * 100m), 2)
            : (double?)null;

        string? signalNote = null;
        if (comparableArticleCount == 0)
        {
            signalNote = "Nema artikala sa prodajom i pre i posle prve nivelacije.";
        }
        else if (comparablePreQuantity < MinimumComparablePreQuantity)
        {
            signalNote = $"Pre-baza je premala za pouzdan pre/post signal ({comparablePreQuantity} kom pre nivelacije).";
        }
        else if ((comparableRevenueCoveragePct ?? 0d) < MinimumComparableCoveragePct)
        {
            signalNote = $"Uporediv pre/post signal pokriva samo {comparableRevenueCoveragePct:0.##}% prometa.";
        }

        var revenueImpactPct = signalNote is null && comparablePreRevenue > 0m
            ? Math.Round((double)((comparablePostRevenue - comparablePreRevenue) / comparablePreRevenue * 100m), 2)
            : (double?)null;

        var unitsImpactPct = signalNote is null && comparablePreQuantity > 0
            ? Math.Round((comparablePostQuantity - comparablePreQuantity) / (double)comparablePreQuantity * 100d, 2)
            : (double?)null;

        return new NivelacijaSplitSnapshot(
            PreRevenue: Math.Round(preRevenue, 2),
            PreQuantity: preQuantity,
            PostRevenue: Math.Round(postRevenue, 2),
            PostQuantity: postQuantity,
            RevenueWithSplit: Math.Round(revenueWithSplit, 2),
            ComparableRevenueWithSplit: Math.Round(comparablePreRevenue + comparablePostRevenue, 2),
            ArticleCountWithNivelacija: articleCountWithNivelacija,
            ComparableArticleCount: comparableArticleCount,
            ComparablePreQuantity: comparablePreQuantity,
            ComparablePostQuantity: comparablePostQuantity,
            RevenueCoveragePct: revenueCoveragePct,
            ComparableRevenueCoveragePct: comparableRevenueCoveragePct,
            RevenueImpactPct: revenueImpactPct,
            UnitsImpactPct: unitsImpactPct,
            HasComparableSignal: revenueImpactPct.HasValue && unitsImpactPct.HasValue,
            SignalNote: signalNote);
    }
}
